using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Reflection;
using Gree.UnityWebView;
using UnityEngine;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// WebView backend powered by net.gree.unity-webview (WebView2 on Windows).
    /// Bitmap capture is driven by <see cref="WebViewPerformanceHub"/> to keep multiple views smooth.
    /// </summary>
    public sealed class GreeUnityWebViewBackend : IWebViewBackend, IWebViewPumpTarget, IWebViewCaptureSuspend
    {
        const string BridgeBootstrapJs =
            "if(!window.Unity){window.Unity={call:function(m){window.location='unity:'+m;}};}";

        readonly Transform _hostTransform;
        readonly WebViewObject _webView;
        readonly FieldInfo _textureField;
        readonly FieldInfo _textureBufferField;
        readonly FieldInfo _webViewHandleField;
        readonly FieldInfo _rectField;
        readonly MethodInfo _setRectMethod;
        readonly MethodInfo _sendMouseEventMethod;
        readonly MethodInfo _getMessageMethod;
        readonly MethodInfo _pluginUpdateMethod;
        readonly MethodInfo _bitmapWidthMethod;
        readonly MethodInfo _bitmapHeightMethod;
        readonly MethodInfo _renderMethod;

        bool _initialized;
        bool _readyMarginsApplied;
        bool _registeredWithHub;
        bool _captureSuspended;
        int _width = 1280;
        int _height = 720;
        int _bitmapRefreshCycle = 1;
        WebViewPumpPriority _priority = WebViewPumpPriority.Runtime;

        public bool IsAvailable { get; private set; }
        public string StatusMessage { get; private set; }
        public bool FlipVertically => true;
        public Texture Texture { get; private set; }
        public WebViewPumpPriority PumpPriority => _priority;
        public int RefreshCycle => _bitmapRefreshCycle;
        public bool IsCaptureSuspended => _captureSuspended;

        public event Action<string> MessageEmitted;
        public event Action<string> UrlChanged;
        public event Action Initialized;

        public GreeUnityWebViewBackend(Transform hostTransform)
        {
            _hostTransform = hostTransform;
            var webViewType = typeof(WebViewObject);
            _textureField = webViewType.GetField("texture", BindingFlags.Instance | BindingFlags.NonPublic);
            _textureBufferField = webViewType.GetField("textureDataBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
            _webViewHandleField = webViewType.GetField("webView", BindingFlags.Instance | BindingFlags.NonPublic);
            _rectField = webViewType.GetField("rect", BindingFlags.Instance | BindingFlags.NonPublic);
            _setRectMethod = webViewType.GetMethod("_CWebViewPlugin_SetRect", BindingFlags.Static | BindingFlags.NonPublic);
            _sendMouseEventMethod = webViewType.GetMethod("_CWebViewPlugin_SendMouseEvent", BindingFlags.Static | BindingFlags.NonPublic);
            _getMessageMethod = webViewType.GetMethod("_CWebViewPlugin_GetMessage", BindingFlags.Static | BindingFlags.NonPublic);
            _pluginUpdateMethod = webViewType.GetMethod("_CWebViewPlugin_Update", BindingFlags.Static | BindingFlags.NonPublic);
            _bitmapWidthMethod = webViewType.GetMethod("_CWebViewPlugin_BitmapWidth", BindingFlags.Static | BindingFlags.NonPublic);
            _bitmapHeightMethod = webViewType.GetMethod("_CWebViewPlugin_BitmapHeight", BindingFlags.Static | BindingFlags.NonPublic);
            _renderMethod = webViewType.GetMethod("_CWebViewPlugin_Render", BindingFlags.Static | BindingFlags.NonPublic);

            try
            {
                var go = new GameObject("UnityWebUI.GreeWebView");
                go.transform.SetParent(_hostTransform, false);
                _webView = go.AddComponent<WebViewObject>();
                _webView.bitmapRefreshCycle = 1;
                _webView.devicePixelRatio = 1;
                _webView.Init(
                    cb: ForwardMessage,
                    hooked: ForwardMessage,
                    ld: _ =>
                    {
                        _webView.SetURLPattern(string.Empty, string.Empty, "^unity:");
                        _webView.EvaluateJS(BridgeBootstrapJs);
                        _initialized = true;
                        Initialized?.Invoke();
                    });
                _webView.SetVisibility(true);
                // Hub drives Update; avoid duplicate native work from MonoBehaviour.Update.
                _webView.enabled = false;
                IsAvailable = true;
                StatusMessage = "gree unity-webview (WebView2) ready.";

                RegisterWithHub();

                if (Application.isPlaying)
                {
                    var runner = _hostTransform.GetComponent<WebViewCoroutineRunner>()
                                 ?? _hostTransform.gameObject.AddComponent<WebViewCoroutineRunner>();
                    runner.StartCoroutine(WaitUntilReady());
                }
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                StatusMessage = $"gree unity-webview init failed: {ex.Message}";
                Debug.LogWarning($"UnityWebUI: {StatusMessage}");
            }
        }

        IEnumerator WaitUntilReady()
        {
            while (_webView != null && !_webView.IsInitialized())
                yield return null;

            ApplyMargins();
            if (!_initialized)
            {
                _initialized = true;
                Initialized?.Invoke();
            }
        }

        void ForwardMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            MessageEmitted?.Invoke(message);
        }

        public void ConfigureDisplay(int bitmapRefreshCycle, WebViewPumpPriority priority = WebViewPumpPriority.Runtime)
        {
            _bitmapRefreshCycle = Mathf.Max(1, bitmapRefreshCycle);
            _priority = priority;
            if (_registeredWithHub)
                RegisterWithHub();
        }

        void RegisterWithHub()
        {
            WebViewPerformanceHub.Unregister(this);
            _registeredWithHub = true;

            if (!_captureSuspended)
                WebViewPerformanceHub.Register(this);

#if !UNITY_EDITOR
            if (Application.isPlaying)
                WebViewGlobalDriver.Ensure();
#endif
        }

        public void SetCaptureSuspended(bool suspended)
        {
            if (_captureSuspended == suspended)
                return;

            _captureSuspended = suspended;
            if (suspended)
            {
                WebViewPerformanceHub.Unregister(this);
                return;
            }

            if (_registeredWithHub)
                WebViewPerformanceHub.Register(this);

            Pump(true);
        }

        public void Attach(Transform hostTransform)
        {
            if (_webView != null && hostTransform != null)
                _webView.transform.SetParent(hostTransform, false);
        }

        public void SetSize(int width, int height)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            ApplyMargins();
        }

        void ApplyMargins()
        {
            if (_webView == null)
                return;

            _webView.SetMargins(0, 0, 0, 0, relative: false);
            SetNativeRect(_width, _height);
        }

        void SetNativeRect(int width, int height)
        {
            if (_webView == null || _setRectMethod == null || _webViewHandleField == null)
                return;

            var handle = _webViewHandleField.GetValue(_webView);
            if (handle is not IntPtr ptr || ptr == IntPtr.Zero)
                return;

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            _setRectMethod.Invoke(null, new object[] { ptr, width, height });
            _rectField?.SetValue(_webView, new Rect(0, 0, width, height));
        }

        public void Pump(bool captureFrame)
        {
            if (_webView == null || !IsAvailable)
                return;

            var ptr = GetNativeHandle();
            if (ptr == IntPtr.Zero)
                return;

            ProcessMessages(ptr);

            if (!_webView.GetVisibility())
            {
                SyncTextureFromWebView();
                return;
            }

            _pluginUpdateMethod?.Invoke(null, new object[] { ptr, captureFrame, _webView.devicePixelRatio });

            if (captureFrame)
                UploadBitmap(ptr);

            TryCompleteReady();
            SyncTextureFromWebView();
        }

        public void SyncDisplay()
        {
        }

        void ProcessMessages(IntPtr ptr)
        {
            if (_getMessageMethod == null || _webView == null)
                return;

            for (;;)
            {
                var raw = _getMessageMethod.Invoke(null, new object[] { ptr }) as string;
                if (string.IsNullOrEmpty(raw))
                    break;

                var separator = raw.IndexOf(':');
                if (separator < 0)
                    continue;

                var payload = raw.Substring(separator + 1);
                switch (raw.Substring(0, separator))
                {
                    case "CallFromJS":
                        _webView.CallFromJS(payload);
                        break;
                    case "CallOnError":
                        _webView.CallOnError(payload);
                        break;
                    case "CallOnHttpError":
                        _webView.CallOnHttpError(payload);
                        break;
                    case "CallOnLoaded":
                        _webView.CallOnLoaded(payload);
                        break;
                    case "CallOnStarted":
                        _webView.CallOnStarted(payload);
                        break;
                    case "CallOnHooked":
                        _webView.CallOnHooked(payload);
                        break;
                    case "CallOnCookies":
                        _webView.CallOnCookies(payload);
                        break;
                }
            }
        }

        void UploadBitmap(IntPtr ptr)
        {
            if (_bitmapWidthMethod == null || _bitmapHeightMethod == null || _renderMethod == null)
                return;

            var width = (int)_bitmapWidthMethod.Invoke(null, new object[] { ptr });
            var height = (int)_bitmapHeightMethod.Invoke(null, new object[] { ptr });
            if (width <= 0 || height <= 0)
                return;

            var texture = _textureField?.GetValue(_webView) as Texture2D;
            var buffer = _textureBufferField?.GetValue(_webView) as byte[];
            var requiredBytes = width * height * 4;

            if (texture == null || texture.width != width || texture.height != height)
            {
                var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false, !linear)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _textureField?.SetValue(_webView, texture);
                buffer = new byte[requiredBytes];
                _textureBufferField?.SetValue(_webView, buffer);
            }
            else if (buffer == null || buffer.Length != requiredBytes)
            {
                buffer = new byte[requiredBytes];
                _textureBufferField?.SetValue(_webView, buffer);
            }

            if (buffer == null || buffer.Length == 0)
                return;

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                _renderMethod.Invoke(null, new object[] { ptr, handle.AddrOfPinnedObject() });
            }
            finally
            {
                handle.Free();
            }

            texture.LoadRawTextureData(buffer);
            texture.Apply(false, false);
        }

        void SyncTextureFromWebView()
        {
            Texture = _textureField?.GetValue(_webView) as Texture;
        }

        void TryCompleteReady()
        {
            if (_webView == null || !_webView.IsInitialized())
                return;

            if (!_readyMarginsApplied)
            {
                ApplyMargins();
                _readyMarginsApplied = true;
            }

            if (_initialized)
                return;

            _initialized = true;
            Initialized?.Invoke();
        }

        IntPtr GetNativeHandle()
        {
            if (_webViewHandleField == null || _webView == null)
                return IntPtr.Zero;

            var handle = _webViewHandleField.GetValue(_webView);
            return handle is IntPtr ptr ? ptr : IntPtr.Zero;
        }

        public void LoadUrl(string url)
        {
            _webView?.LoadURL(url);
        }

        public void PostMessage(string message)
        {
            _webView?.EvaluateJS($"if(window.vuplex)window.vuplex.postMessage({JsonString(message)});");
        }

        public void Click(int x, int y)
        {
            SendMouseEvent(x, y, 0f, 1);
            SendMouseEvent(x, y, 0f, 3);
        }

        public void PointerDown(int x, int y)
        {
            SendMouseEvent(x, y, 0f, 1);
        }

        public void PointerUp(int x, int y)
        {
            SendMouseEvent(x, y, 0f, 3);
        }

        public void MovePointer(int x, int y)
        {
            SendMouseEvent(x, y, 0f, 0);
        }

        public void LeavePointer()
        {
            SendMouseEvent(-1, -1, 0f, 0);
        }

        public void Scroll(int deltaX, int deltaY)
        {
            SendMouseEvent(_width / 2, _height / 2, deltaY, 0);
        }

        void SendMouseEvent(int x, int y, float scrollDelta, int mouseState)
        {
            var ptr = GetNativeHandle();
            if (ptr == IntPtr.Zero || _sendMouseEventMethod == null)
                return;

            _sendMouseEventMethod.Invoke(null, new object[] { ptr, x, y, scrollDelta, mouseState });
        }

        public void Tick()
        {
            // Kept for interface compatibility; hub is the single pump entry point.
            Pump(true);
        }

        public void ExecuteJavaScript(string script)
        {
            _webView?.EvaluateJS(script);
        }

        static string JsonString(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "''";
            return "'" + raw.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        }

        public void Dispose()
        {
            WebViewPerformanceHub.Unregister(this);
            _registeredWithHub = false;

            if (_webView != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_webView.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(_webView.gameObject);
            }

            Texture = null;
            _initialized = false;
            _readyMarginsApplied = false;
        }
    }
}

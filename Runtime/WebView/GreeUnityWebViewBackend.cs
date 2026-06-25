using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Optional fallback backend for net.gree.unity-webview. Compiles without that package installed.
    /// </summary>
    public sealed class GreeUnityWebViewBackend : IWebViewBackend, IWebViewPumpTarget, IWebViewCaptureSuspend,
        IWebViewJavaScriptExecutor, IWebViewDisplayConfigurable
    {
        readonly Transform _hostTransform;
        readonly GreeWebViewObjectProxy _webView;
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

            if (!GreeWebViewObjectProxy.IsPackagePresent)
            {
                IsAvailable = false;
                StatusMessage = "gree package not installed (optional fallback).";
                return;
            }

            try
            {
                _webView = GreeWebViewObjectProxy.TryCreate(_hostTransform, ForwardMessage, out var error);
                if (_webView == null)
                {
                    IsAvailable = false;
                    StatusMessage = error ?? "gree unity-webview init failed.";
                    return;
                }

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
            if (message == null)
            {
                _initialized = true;
                Initialized?.Invoke();
                return;
            }

            if (string.IsNullOrEmpty(message))
                return;

            MessageEmitted?.Invoke(message);
        }

        public void ConfigureDisplay(int bitmapRefreshCycle, WebViewPumpPriority priority = WebViewPumpPriority.Runtime, float renderScale = 1f)
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
            _webView?.SetParent(hostTransform);
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
            if (_webView == null || _webView.SetRectMethod == null || _webView.WebViewHandleField == null)
                return;

            var handle = _webView.WebViewHandleField.GetValue(_webView.Component);
            if (handle is not IntPtr ptr || ptr == IntPtr.Zero)
                return;

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            _webView.SetRectMethod.Invoke(null, new object[] { ptr, width, height });
            _webView.RectField?.SetValue(_webView.Component, new Rect(0, 0, width, height));
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

            _webView.PluginUpdateMethod?.Invoke(null, new object[] { ptr, captureFrame, _webView.DevicePixelRatio });

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
            if (_webView?.GetMessageMethod == null)
                return;

            for (;;)
            {
                var raw = _webView.GetMessageMethod.Invoke(null, new object[] { ptr }) as string;
                if (string.IsNullOrEmpty(raw))
                    break;

                var separator = raw.IndexOf(':');
                if (separator < 0)
                    continue;

                var payload = raw.Substring(separator + 1);
                switch (raw.Substring(0, separator))
                {
                    case "CallFromJS":
                        _webView.CallFromJsMethod?.Invoke(_webView.Component, new object[] { payload });
                        break;
                    case "CallOnError":
                        _webView.CallOnErrorMethod?.Invoke(_webView.Component, new object[] { payload });
                        break;
                    case "CallOnHttpError":
                        _webView.CallOnHttpErrorMethod?.Invoke(_webView.Component, new object[] { payload });
                        break;
                    case "CallOnLoaded":
                        _webView.CallOnLoadedMethod?.Invoke(_webView.Component, new object[] { payload });
                        break;
                    case "CallOnStarted":
                        _webView.CallOnStartedMethod?.Invoke(_webView.Component, new object[] { payload });
                        break;
                    case "CallOnHooked":
                        _webView.CallOnHookedMethod?.Invoke(_webView.Component, new object[] { payload });
                        break;
                    case "CallOnCookies":
                        _webView.CallOnCookiesMethod?.Invoke(_webView.Component, new object[] { payload });
                        break;
                }
            }
        }

        void UploadBitmap(IntPtr ptr)
        {
            if (_webView?.BitmapWidthMethod == null || _webView.BitmapHeightMethod == null || _webView.RenderMethod == null)
                return;

            var width = (int)_webView.BitmapWidthMethod.Invoke(null, new object[] { ptr });
            var height = (int)_webView.BitmapHeightMethod.Invoke(null, new object[] { ptr });
            if (width <= 0 || height <= 0)
                return;

            var texture = _webView.TextureField?.GetValue(_webView.Component) as Texture2D;
            var buffer = _webView.TextureBufferField?.GetValue(_webView.Component) as byte[];
            var requiredBytes = width * height * 4;

            if (texture == null || texture.width != width || texture.height != height)
            {
                var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false, !linear)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _webView.TextureField?.SetValue(_webView.Component, texture);
                buffer = new byte[requiredBytes];
                _webView.TextureBufferField?.SetValue(_webView.Component, buffer);
            }
            else if (buffer == null || buffer.Length != requiredBytes)
            {
                buffer = new byte[requiredBytes];
                _webView.TextureBufferField?.SetValue(_webView.Component, buffer);
            }

            if (buffer == null || buffer.Length == 0)
                return;

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                _webView.RenderMethod.Invoke(null, new object[] { ptr, handle.AddrOfPinnedObject() });
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
            Texture = _webView?.TextureField?.GetValue(_webView.Component) as Texture;
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
            if (_webView?.WebViewHandleField == null)
                return IntPtr.Zero;

            var handle = _webView.WebViewHandleField.GetValue(_webView.Component);
            return handle is IntPtr ptr ? ptr : IntPtr.Zero;
        }

        public void LoadUrl(string url)
        {
            _webView?.LoadUrl(url);
        }

        public void PostMessage(string message)
        {
            _webView?.EvaluateJs($"if(window.vuplex)window.vuplex.postMessage({JsonString(message)});");
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
            if (ptr == IntPtr.Zero || _webView?.SendMouseEventMethod == null)
                return;

            _webView.SendMouseEventMethod.Invoke(null, new object[] { ptr, x, y, scrollDelta, mouseState });
        }

        public void Tick()
        {
            Pump(true);
        }

        public void ExecuteJavaScript(string script)
        {
            _webView?.EvaluateJs(script);
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

            var go = _webView?.GameObject;
            if (go != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(go);
                else
                    UnityEngine.Object.DestroyImmediate(go);
            }

            Texture = null;
            _initialized = false;
            _readyMarginsApplied = false;
        }
    }
}

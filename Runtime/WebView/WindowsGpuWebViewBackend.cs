using System;
using System.Runtime.InteropServices;
using AOT;
using UnityEngine;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Windows-only WebView2 backend using D3D11 GPU capture (native plugin).
    /// Falls back to gree when the DLL is missing or graphics API is not D3D11.
    /// </summary>
    public sealed class WindowsGpuWebViewBackend : IWebViewBackend, IWebViewPumpTarget, IWebViewCaptureSuspend,
        IWebViewJavaScriptExecutor, IWebViewDisplayConfigurable
    {
        readonly Transform _hostTransform;
        readonly GCHandle _selfHandle;
        readonly int _handle;

        Texture2D _externalTexture;
        Texture2D _cpuTexture;
        byte[] _cpuBuffer;
        int _width = 1280;
        int _height = 720;
        int _bitmapRefreshCycle = 1;
        float _renderScale = 1f;
        bool _initialized;
        bool _registeredWithHub;
        bool _captureSuspended;
        WebViewPumpPriority _priority = WebViewPumpPriority.Runtime;

        public bool IsAvailable { get; }
        public string StatusMessage { get; }
        public bool FlipVertically => true;
        public Texture Texture
        {
            get
            {
                if (_handle > 0 &&
                    WindowsGpuWebViewNative.WebViewGpu_HasGpuFrame(_handle) != 0 &&
                    _externalTexture != null)
                    return _externalTexture;

                return _cpuTexture != null ? _cpuTexture : _externalTexture;
            }
        }
        public WebViewPumpPriority PumpPriority => _priority;
        public int RefreshCycle => _bitmapRefreshCycle;
        public int GpuHandle => _handle;
        public float RenderScale => _renderScale;
        public bool IsCaptureSuspended => _captureSuspended;
        public int ContentVersion { get; private set; }

        public event Action FrameUpdated;

        public event Action<string> MessageEmitted;
        public event Action<string> UrlChanged;
        public event Action Initialized;

        public WindowsGpuWebViewBackend(Transform hostTransform, bool transparent = false)
        {
            _hostTransform = hostTransform;

            if (!WindowsGpuWebViewNative.TryLoad(out var loadReason))
            {
                IsAvailable = false;
                StatusMessage = loadReason ?? "UnityWebUI.WebView2Gpu.dll not found or D3D11 not active.";
                _handle = -1;
                return;
            }

            _selfHandle = GCHandle.Alloc(this);
            _handle = WindowsGpuWebViewNative.WebViewGpu_Create(_width, _height, transparent ? 1 : 0);
            if (_handle <= 0)
            {
                IsAvailable = false;
                StatusMessage = "WebViewGpu_Create failed.";
                _selfHandle.Free();
                return;
            }

            WindowsGpuWebViewNative.WebViewGpu_SetMessageCallback(_handle, OnNativeMessage, GCHandle.ToIntPtr(_selfHandle));
            IsAvailable = true;
            StatusMessage = "Windows GPU WebView2 ready (D3D11 shared texture).";
            RegisterWithHub();
        }

        [MonoPInvokeCallback(typeof(WindowsGpuWebViewNative.MessageCallback))]
        static void OnNativeMessage(string jsonUtf8, IntPtr userData)
        {
            if (userData == IntPtr.Zero || string.IsNullOrEmpty(jsonUtf8))
                return;

            var handle = GCHandle.FromIntPtr(userData);
            if (handle.Target is WindowsGpuWebViewBackend backend)
                backend.MessageEmitted?.Invoke(jsonUtf8);
        }

        public void ConfigureDisplay(int bitmapRefreshCycle, WebViewPumpPriority priority = WebViewPumpPriority.Runtime, float renderScale = 1f)
        {
            _bitmapRefreshCycle = Mathf.Max(1, bitmapRefreshCycle);
            _priority = priority;
            _renderScale = Mathf.Clamp(renderScale, 0.25f, 1f);
            if (_handle > 0)
                WindowsGpuWebViewNative.WebViewGpu_SetRenderScale(_handle, _renderScale);
            if (_registeredWithHub)
                RegisterWithHub();
        }

        void RegisterWithHub()
        {
            WebViewPerformanceHub.Unregister(this);
            _registeredWithHub = true;

            if (!_captureSuspended)
                WebViewPerformanceHub.Register(this);

            if (Application.isPlaying)
                WebViewGlobalDriver.Ensure();
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
            SyncDisplay();
        }

        public void Attach(Transform hostTransform)
        {
            // GPU backend has no scene hierarchy object.
        }

        public void SetSize(int width, int height)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            if (_handle > 0)
                WindowsGpuWebViewNative.WebViewGpu_Resize(_handle, _width, _height);
        }

        public void LoadUrl(string url)
        {
            if (_handle > 0)
                WindowsGpuWebViewNative.WebViewGpu_LoadUrl(_handle, url);
        }

        public void PostMessage(string message)
        {
            ExecuteJavaScript($"window.dispatchEvent(new MessageEvent('message',{{data:{JsonString(message)}}}));");
        }

        public void Click(int x, int y)
        {
            if (_handle > 0)
                WindowsGpuWebViewNative.WebViewGpu_Click(_handle, x, y);
        }

        public void PointerDown(int x, int y)
        {
            if (_handle > 0)
                WindowsGpuWebViewNative.WebViewGpu_PointerDown(_handle, x, y);
        }

        public void PointerUp(int x, int y)
        {
            if (_handle > 0)
                WindowsGpuWebViewNative.WebViewGpu_PointerUp(_handle, x, y);
        }

        public void MovePointer(int x, int y)
        {
            if (_handle > 0)
                WindowsGpuWebViewNative.WebViewGpu_PointerMove(_handle, x, y);
        }

        public void LeavePointer()
        {
            if (_handle > 0)
                WindowsGpuWebViewNative.WebViewGpu_PointerLeave(_handle);
        }

        public void Scroll(int deltaX, int deltaY)
        {
            if (_handle > 0)
                WindowsGpuWebViewNative.WebViewGpu_Scroll(_handle, _width / 2, _height / 2, deltaY);
        }

        public void SendKey(int virtualKeyCode, bool keyDown)
        {
            if (_handle > 0)
                WindowsGpuWebViewNative.WebViewGpu_SendKey(_handle, virtualKeyCode, keyDown ? 1 : 0);
        }

        public void ExecuteJavaScript(string script)
        {
            if (_handle > 0 && !string.IsNullOrEmpty(script))
                WindowsGpuWebViewNative.WebViewGpu_ExecuteJavaScript(_handle, script);
        }

        public void Pump(bool captureFrame)
        {
            if (_handle <= 0)
                return;

            WindowsGpuWebViewNative.WebViewGpu_Tick(_handle, captureFrame ? 1 : 0);

            if (!_initialized && WindowsGpuWebViewNative.WebViewGpu_IsInitialized(_handle) != 0)
            {
                _initialized = true;
                ExecuteJavaScript(WebBridgeScript.GetBootstrapJavaScript());
                Initialized?.Invoke();
            }
        }

        public void SyncDisplay()
        {
            SyncTexture();
        }

        void SyncTexture()
        {
            if (_handle <= 0)
                return;

            WindowsGpuWebViewNative.WebViewGpu_GetRuntimeStatus(
                _handle, out _, out _, out _, out var deviceReady);

            // GPU shared texture once WGC copy succeeded on the render thread.
            if (deviceReady != 0 &&
                WindowsGpuWebViewNative.WebViewGpu_HasGpuFrame(_handle) != 0 &&
                SyncExternalTexture())
            {
                ReleaseCpuCaptureResources();
                NotifyFrameUpdated();
                return;
            }

            // CPU fallback while GPU frame is not ready, or when capture fails.
            if (TrySyncCpuTexture())
                NotifyFrameUpdated();
        }

        void NotifyFrameUpdated()
        {
            ContentVersion++;
            FrameUpdated?.Invoke();
        }

        void ReleaseCpuCaptureResources()
        {
            if (_cpuTexture == null)
                return;

            UnityEngine.Object.Destroy(_cpuTexture);
            _cpuTexture = null;
            _cpuBuffer = null;
        }

        bool TrySyncCpuTexture()
        {
            if (WindowsGpuWebViewNative.WebViewGpu_HasCpuFrame(_handle) == 0)
                return false;

            var required = WindowsGpuWebViewNative.WebViewGpu_CopyCpuFrame(_handle, Array.Empty<byte>(), 0, out var w, out var h);
            if (required <= 0 || w <= 0 || h <= 0)
                return false;

            if (_cpuBuffer == null || _cpuBuffer.Length != required)
                _cpuBuffer = new byte[required];

            WindowsGpuWebViewNative.WebViewGpu_CopyCpuFrame(_handle, _cpuBuffer, _cpuBuffer.Length, out w, out h);
            if (_cpuTexture == null || _cpuTexture.width != w || _cpuTexture.height != h)
            {
                _cpuTexture = new Texture2D(w, h, TextureFormat.BGRA32, false, false);
                _cpuTexture.filterMode = FilterMode.Bilinear;
                _cpuTexture.wrapMode = TextureWrapMode.Clamp;
            }

            var raw = _cpuTexture.GetRawTextureData<byte>();
            if (raw.Length != required)
                return false;

            raw.CopyFrom(_cpuBuffer);
            _cpuTexture.Apply(false);
            return true;
        }

        bool SyncExternalTexture()
        {
            if (_handle <= 0)
                return false;

            var nativePtr = WindowsGpuWebViewNative.WebViewGpu_GetTexturePointer(_handle);
            if (nativePtr == IntPtr.Zero)
                return false;

            var w = WindowsGpuWebViewNative.WebViewGpu_GetTextureWidth(_handle);
            var h = WindowsGpuWebViewNative.WebViewGpu_GetTextureHeight(_handle);
            if (w <= 0 || h <= 0)
                return false;

            if (_externalTexture == null || _externalTexture.width != w || _externalTexture.height != h)
            {
                _externalTexture = Texture2D.CreateExternalTexture(w, h, TextureFormat.BGRA32, false, false, nativePtr);
                _externalTexture.filterMode = FilterMode.Bilinear;
                _externalTexture.wrapMode = TextureWrapMode.Clamp;
                return true;
            }

            _externalTexture.UpdateExternalTexture(nativePtr);
            return true;
        }

        public void Tick()
        {
            Pump(true);
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

            if (_handle > 0)
            {
                WindowsGpuWebViewNative.WebViewGpu_SetMessageCallback(_handle, null, IntPtr.Zero);
                WindowsGpuWebViewNative.WebViewGpu_Destroy(_handle);
            }

            if (_externalTexture != null)
            {
                UnityEngine.Object.Destroy(_externalTexture);
                _externalTexture = null;
            }

            if (_cpuTexture != null)
            {
                UnityEngine.Object.Destroy(_cpuTexture);
                _cpuTexture = null;
            }

            _cpuBuffer = null;

            if (_selfHandle.IsAllocated)
                _selfHandle.Free();

            _initialized = false;
        }
    }
}

using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Runtime host: embed a WebView in a scene. Browser handles all visual CSS; Unity receives bridge events.
    /// </summary>
    public sealed class WebViewHost : MonoBehaviour
    {
        [SerializeField] int _width = 1920;
        [SerializeField] int _height = 1080;
        [Tooltip("Capture interval (1 = every scheduled frame). Multiple WebViews auto-stagger via the performance hub.")]
        [SerializeField] int _bitmapRefreshCycle = 1;
        [Tooltip("Internal render scale for GPU backend (0.25-1). Lower = faster.")]
        [SerializeField] [Range(0.25f, 1f)] float _renderScale = 1f;
        [SerializeField] bool _transparentBackground;
        [SerializeField] WebUIViewBindingProfile _bindingProfile;

        [Header("Play Mode")]
        [Tooltip("RawImage that shows the page. Auto-wires pointer clicks to the WebView.")]
        [SerializeField] RawImage _display;
        [Tooltip("Load Binding Profile Source HTML on Start (when path is set).")]
        [SerializeField] bool _loadProfileHtmlOnStart = true;
        [Tooltip("Optional HTML path override (absolute or under StreamingAssets).")]
        [SerializeField] string _htmlPathOverride;
        [Tooltip("When false, page starts hidden: no capture until SetPageVisible(true) (ESC menu pattern).")]
        [SerializeField] bool _visibleOnStart = true;

        IWebViewBackend _backend;
        WebViewLoadOptions _options = new WebViewLoadOptions();
        WebViewPointerRelay _pointerRelay;
        bool _started;
        bool _pageVisible = true;
        Coroutine _bridgeInjectRoutine;

        public WebViewBridge Bridge { get; } = new WebViewBridge();
        public IWebViewBackend Backend => _backend;
        public WebUIViewBindingProfile BindingProfile => _bindingProfile;
        public bool IsPageVisible => _pageVisible;
        public Texture ViewTexture => _backend?.Texture;
        public string Status => _backend?.StatusMessage;

        void Awake()
        {
            _backend = WebViewBackendFactory.Create(transform, new WebViewBackendOptions
            {
                BitmapRefreshCycle = _bitmapRefreshCycle,
                Priority = WebViewPumpPriority.Runtime,
                RenderScale = _renderScale,
                TransparentBackground = _transparentBackground,
            });

            _backend.MessageEmitted += Bridge.HandleRawMessage;
            _backend.Initialized += OnBackendInitialized;
            _backend.Attach(transform);
            _backend.SetSize(_width, _height);

            _pageVisible = _visibleOnStart;
            if (!_pageVisible && _backend is IWebViewCaptureSuspend suspend)
                suspend.SetCaptureSuspended(true);
        }

        void Start()
        {
            StartCoroutine(StartAfterLayout());
        }

        IEnumerator StartAfterLayout()
        {
            Canvas.ForceUpdateCanvases();
            yield return null;

            if (_started)
                yield break;

            _started = true;
            EnsureActionDispatcher();
            WireDisplayInput();
            TryLoadInitialHtml();
            ApplyPageDisplayState(_pageVisible);
        }

        void Update()
        {
            if (!_pageVisible || _display == null || _backend?.Texture == null)
                return;

            WebViewDisplayUtility.ApplyToRawImage(_display, _backend);
        }

        /// <summary>
        /// Show or hide the page. Hidden: WebView stays loaded but capture/input stop (no per-frame cost).
        /// </summary>
        public void SetPageVisible(bool visible)
        {
            if (_pageVisible == visible)
                return;

            _pageVisible = visible;

            if (_backend is IWebViewCaptureSuspend suspend)
                suspend.SetCaptureSuspended(!visible);

            ApplyPageDisplayState(visible);
        }

        void ApplyPageDisplayState(bool visible)
        {
            if (_display != null)
            {
                _display.enabled = visible;
                if (visible && _backend != null)
                    WebViewDisplayUtility.ApplyToRawImage(_display, _backend);
            }

            if (_pointerRelay != null)
                _pointerRelay.enabled = visible;
        }

        public void SetDisplay(RawImage display)
        {
            _display = display;
            WireDisplayInput();
        }

        void EnsureActionDispatcher()
        {
            var dispatcher = GetComponent<WebViewActionDispatcher>();
            if (dispatcher == null)
                dispatcher = gameObject.AddComponent<WebViewActionDispatcher>();

            if (_bindingProfile != null && dispatcher.Profile == null)
                dispatcher.Profile = _bindingProfile;
        }

        void WireDisplayInput()
        {
            if (_display == null)
                return;

            _display.raycastTarget = true;
            _pointerRelay = _display.GetComponent<WebViewPointerRelay>();
            if (_pointerRelay == null)
                _pointerRelay = _display.gameObject.AddComponent<WebViewPointerRelay>();

            _pointerRelay.Bind(this, _display.rectTransform);
            ApplyCaptureSizeFromDisplay();
        }

        void ApplyCaptureSizeFromDisplay()
        {
            if (_display == null)
                return;

            var rect = _display.rectTransform.rect;
            var w = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            var h = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            if (w > 1 && h > 1)
                SetSize(w, h);
        }

        void TryLoadInitialHtml()
        {
            if (_backend == null || !_backend.IsAvailable)
                return;

            if (!string.IsNullOrWhiteSpace(_htmlPathOverride))
            {
                var overridePath = ResolveHtmlPath(_htmlPathOverride);
                if (File.Exists(overridePath))
                {
                    LoadLocalHtml(overridePath);
                    return;
                }
            }

            if (!_loadProfileHtmlOnStart || _bindingProfile == null)
                return;

            var profilePath = _bindingProfile.SourceHtmlPath;
            if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
                return;

            LoadLocalHtml(profilePath);
        }

        static string ResolveHtmlPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            var underStreaming = Path.Combine(Application.streamingAssetsPath, path);
            if (File.Exists(underStreaming))
                return Path.GetFullPath(underStreaming);

            return Path.GetFullPath(path);
        }

        void OnDestroy()
        {
            if (_backend == null)
                return;

            _backend.MessageEmitted -= Bridge.HandleRawMessage;
            _backend.Initialized -= OnBackendInitialized;
            _backend.Dispose();
            _backend = null;
        }

        public void Configure(WebViewLoadOptions options)
        {
            _options = options ?? new WebViewLoadOptions();
        }

        public void SetSize(int width, int height)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            _backend?.SetSize(_width, _height);
        }

        public void SetBitmapRefreshCycle(int cycle)
        {
            _bitmapRefreshCycle = Mathf.Max(1, cycle);
            ApplyBackendDisplayOptions();
        }

        public void SetRenderScale(float scale)
        {
            _renderScale = Mathf.Clamp(scale, 0.25f, 1f);
            ApplyBackendDisplayOptions();
        }

        public void SetBindingProfile(WebUIViewBindingProfile profile)
        {
            _bindingProfile = profile;
        }

        void ApplyBackendDisplayOptions()
        {
            switch (_backend)
            {
                case GreeUnityWebViewBackend gree:
                    gree.ConfigureDisplay(_bitmapRefreshCycle, WebViewPumpPriority.Runtime);
                    break;
                case WindowsGpuWebViewBackend gpu:
                    gpu.ConfigureDisplay(_bitmapRefreshCycle, WebViewPumpPriority.Runtime, _renderScale);
                    break;
            }
        }

        public void LoadLocalHtml(string htmlFilePath)
        {
            if (_backend == null)
                return;

            var resolved = LocalPageUrl.ResolveHtmlPath(htmlFilePath, _options.BaseDirectory);
            _options.BaseDirectory = Path.GetDirectoryName(resolved);
            _backend.LoadUrl(LocalPageUrl.ToFileUrl(resolved));
            ScheduleBridgeInjection();
        }

        void ScheduleBridgeInjection()
        {
            if (!_options.InjectBridgeScript || _backend == null)
                return;

            if (_bridgeInjectRoutine != null)
                StopCoroutine(_bridgeInjectRoutine);

            _bridgeInjectRoutine = StartCoroutine(InjectBridgeAfterNavigation());
        }

        IEnumerator InjectBridgeAfterNavigation()
        {
            InjectBridgeScript();
            for (var i = 0; i < 24; i++)
            {
                yield return new WaitForSecondsRealtime(0.125f);
                if (_backend == null)
                    yield break;

                InjectBridgeScript();
            }

            _bridgeInjectRoutine = null;
        }

        void InjectBridgeScript()
        {
            if (!_options.InjectBridgeScript || _backend == null)
                return;

            WebBridgeScript.ExecuteJavaScript(_backend, WebBridgeScript.GetBootstrapJavaScript());
        }

        void OnBackendInitialized()
        {
            if (!_options.InjectBridgeScript)
                return;

            InjectBridgeScript();
        }

        public void ForwardClick(Vector2 localPosition)
        {
            _backend?.Click(Mathf.RoundToInt(localPosition.x), Mathf.RoundToInt(localPosition.y));
        }

        public void ForwardPointerDown(Vector2 localPosition)
        {
            _backend?.PointerDown(Mathf.RoundToInt(localPosition.x), Mathf.RoundToInt(localPosition.y));
        }

        public void ForwardPointerUp(Vector2 localPosition)
        {
            _backend?.PointerUp(Mathf.RoundToInt(localPosition.x), Mathf.RoundToInt(localPosition.y));
            WebViewDomClickUtility.SynthesizeClick(_backend, localPosition);
        }

        public void ForwardPointerMove(Vector2 localPosition)
        {
            _backend?.MovePointer(Mathf.RoundToInt(localPosition.x), Mathf.RoundToInt(localPosition.y));
        }

        public void ForwardPointerLeave()
        {
            _backend?.LeavePointer();
        }

        public void ForwardScroll(Vector2 delta)
        {
            _backend?.Scroll(Mathf.RoundToInt(delta.x), Mathf.RoundToInt(delta.y));
        }

        public void ForwardKey(KeyCode key, bool keyDown)
        {
            if (_backend is WindowsGpuWebViewBackend gpu)
                gpu.SendKey((int)key, keyDown);
        }
    }
}

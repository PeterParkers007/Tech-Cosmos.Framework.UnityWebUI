using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Runtime test: assign an HTML path, press Play — page renders on the linked RawImage.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WebViewHost))]
    public sealed class WebViewRuntimeHtmlTest : MonoBehaviour
    {
        [Header("HTML")]
        [Tooltip("Absolute path, or relative to StreamingAssets (e.g. WebUI/sample/index.html). Empty = sample page.")]
        [SerializeField] string _htmlPath;

        [Header("UI")]
        [SerializeField] RawImage _display;
        [Tooltip("Match WebViewHost capture size to this RectTransform (recommended).")]
        [SerializeField] RectTransform _sizeSource;

        [Header("Options")]
        [SerializeField] bool _logBridgeEvents;
        [SerializeField] bool _logGpuStatusOnce = true;

        WebViewHost _host;
        WebViewPointerRelay _pointerRelay;
        bool _gpuStatusLogged;

        void Awake()
        {
            _host = GetComponent<WebViewHost>();
            if (_display == null)
                _display = GetComponentInChildren<RawImage>(true);

            if (_sizeSource == null && _display != null)
                _sizeSource = _display.rectTransform;

            if (_display != null)
            {
                _pointerRelay = _display.GetComponent<WebViewPointerRelay>();
                if (_pointerRelay == null)
                    _pointerRelay = _display.gameObject.AddComponent<WebViewPointerRelay>();
                _pointerRelay.Bind(_host, _sizeSource != null ? _sizeSource : _display.rectTransform);
            }

            if (_display != null)
                _host.SetDisplay(_display);
        }

        void Start()
        {
            StartCoroutine(StartAfterLayout());
        }

        IEnumerator StartAfterLayout()
        {
            Canvas.ForceUpdateCanvases();
            yield return null;

            if (_host.Backend == null || !_host.Backend.IsAvailable)
            {
                Debug.LogError(
                    $"[WebViewRuntimeHtmlTest] WebView backend unavailable: {_host.Status}\n" +
                    WindowsGpuWebViewNative.ProbeDiagnostics());
                enabled = false;
                yield break;
            }

            if (_display == null)
            {
                Debug.LogError("[WebViewRuntimeHtmlTest] RawImage (Display) is not assigned.");
                enabled = false;
                yield break;
            }

            var resolved = ResolveHtmlPath(_htmlPath);
            if (!File.Exists(resolved))
            {
                Debug.LogError($"[WebViewRuntimeHtmlTest] HTML not found: {resolved}");
                enabled = false;
                yield break;
            }

            if (_logBridgeEvents)
            {
                _host.Bridge.ActionClicked += id => Debug.Log($"[WebViewRuntimeHtmlTest] action: {id}");
                _host.Bridge.NavigateRequested += href => Debug.Log($"[WebViewRuntimeHtmlTest] navigate: {href}");
            }

            ApplyCaptureSize();
            _host.LoadLocalHtml(resolved);
            Debug.Log($"[WebViewRuntimeHtmlTest] Loading: {resolved} | backend={_host.Backend.GetType().Name} | size={_host.Backend.Texture?.width}x{_host.Backend.Texture?.height}");
        }

        void Update()
        {
            if (_display == null || _host.Backend?.Texture == null)
                return;

            if (_logGpuStatusOnce && _host.Backend is WindowsGpuWebViewBackend gpu)
            {
                _logGpuStatusOnce = false;
                var status = WindowsGpuWebViewNative.GetRuntimeStatus(gpu.GpuHandle);
                Debug.Log($"[WebViewRuntimeHtmlTest] GPU status: {status}");
                Debug.Log($"[WebViewRuntimeHtmlTest] {WindowsGpuWebViewNative.GetPluginDiagnosticsText()} | unityGraphics={SystemInfo.graphicsDeviceType}");
                if (status.Contains("device=0"))
                {
                    Debug.LogWarning(
                        "[WebViewRuntimeHtmlTest] GPU device not bound.\n" +
                        "• Project Settings → Player → Windows → put Direct3D11 first (remove D3D12/Vulkan)\n" +
                        "• Apply api=8 plugin (apply-gpu-plugin.bat after closing Unity)\n" +
                        "• Expected: pluginLoad=1 renderer=D3D11 hasD3D11=1 device=1 api=8");
                }
            }
        }

        void ApplyCaptureSize()
        {
            if (_sizeSource == null)
                return;

            Canvas.ForceUpdateCanvases();
            var size = _sizeSource.rect.size;
            var w = Mathf.Max(1, Mathf.RoundToInt(size.x));
            var h = Mathf.Max(1, Mathf.RoundToInt(size.y));
            if (w <= 1 || h <= 1)
                Debug.LogWarning($"[WebViewRuntimeHtmlTest] Capture size is very small ({w}x{h}). Check RawImage layout.");
            _host.SetSize(w, h);
        }

        static string ResolveHtmlPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Path.Combine(Application.streamingAssetsPath, "WebUI", "sample", "index.html");

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            var underStreaming = Path.Combine(Application.streamingAssetsPath, path);
            if (File.Exists(underStreaming))
                return Path.GetFullPath(underStreaming);

            return Path.GetFullPath(path);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_display != null)
                _display.raycastTarget = true;
        }
#endif
    }
}

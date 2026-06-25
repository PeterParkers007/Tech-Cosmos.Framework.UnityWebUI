using UnityEngine;

namespace UnityWebUI.WebView
{
    public static class WebViewBackendFactory
    {
        public static string LastGpuFallbackReason { get; private set; }

        public static bool IsWindowsGpuPlatform =>
            Application.platform == RuntimePlatform.WindowsEditor ||
            Application.platform == RuntimePlatform.WindowsPlayer;

        public static IWebViewBackend Create(Transform hostTransform, WebViewBackendOptions options = null)
        {
            options ??= WebViewBackendOptions.Default;
            LastGpuFallbackReason = null;

            if (hostTransform == null)
            {
                var go = new GameObject("UnityWebUI.WebViewHost");
                hostTransform = go.transform;
            }

            if (IsWindowsGpuPlatform)
            {
                var gpu = TryCreateGpu(hostTransform, options);
                if (gpu != null)
                    return gpu;
            }

            var gree = new GreeUnityWebViewBackend(hostTransform);
            if (gree.IsAvailable)
            {
                if (gree is IWebViewDisplayConfigurable configurable)
                    configurable.ConfigureDisplay(options.BitmapRefreshCycle, options.Priority);
                return gree;
            }

            gree.Dispose();

            var vuplex = new VuplexWebViewBackend(hostTransform);
            if (vuplex.IsAvailable)
                return vuplex;

            vuplex.Dispose();
            return new NullWebViewBackend(
                "WebView 未就绪。Windows：确认 Plugins/Windows/x86_64/UnityWebUI.WebView2Gpu.dll 存在，Graphics API 为 D3D11，并已安装 WebView2 Runtime。\n" +
                "可选：Window → Unity Web UI → Setup Project，或安装 net.gree.unity-webview 作为 CPU 回退。");
        }

        static IWebViewBackend TryCreateGpu(Transform hostTransform, WebViewBackendOptions options)
        {
            if (!WindowsGpuWebViewNative.IsDllPresent())
            {
                LastGpuFallbackReason = "gpu dll missing → build: Window/Unity Web UI/Build Windows GPU Plugin";
                return null;
            }

            try
            {
                if (!WindowsGpuWebViewNative.TryLoad(out var loadFailure))
                {
                    LastGpuFallbackReason = loadFailure ?? "gpu load failed";
                    return null;
                }

                var gpu = new WindowsGpuWebViewBackend(hostTransform, options.TransparentBackground);
                if (!gpu.IsAvailable)
                {
                    LastGpuFallbackReason = gpu.StatusMessage;
                    gpu.Dispose();
                    return null;
                }

                gpu.ConfigureDisplay(options.BitmapRefreshCycle, options.Priority, options.RenderScale);
                return gpu;
            }
            catch (System.DllNotFoundException ex)
            {
                LastGpuFallbackReason = ex.Message;
                return null;
            }
        }
    }
}

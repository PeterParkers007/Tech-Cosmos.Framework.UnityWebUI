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
                gree.ConfigureDisplay(options.BitmapRefreshCycle, options.Priority);
                return gree;
            }

            gree.Dispose();

            var vuplex = new VuplexWebViewBackend(hostTransform);
            if (vuplex.IsAvailable)
                return vuplex;

            vuplex.Dispose();
            return new NullWebViewBackend(
                "WebView 依赖未就绪。Windows GPU: 编译 Native/Windows/build.bat；" +
                "或安装 net.gree.unity-webview + WebView2 Runtime。");
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

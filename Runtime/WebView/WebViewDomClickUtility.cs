using UnityEngine;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// CDP pointer down/up does not produce DOM click; emit unity-bridge action after pointer up.
    /// </summary>
    public static class WebViewDomClickUtility
    {
        public static void SynthesizeClick(IWebViewBackend backend, int x, int y)
        {
            if (backend == null || backend is VuplexWebViewBackend)
                return;

            var mappedX = x;
            var mappedY = y;
            if (backend is WindowsGpuWebViewBackend gpuBackend)
            {
                var scale = gpuBackend.RenderScale;
                mappedX = Mathf.RoundToInt(x * scale);
                mappedY = Mathf.RoundToInt(y * scale);
            }

            WebBridgeScript.EnsureInstalled(backend);
            WebBridgeScript.ExecuteJavaScript(backend, WebBridgeScript.GetActionAtPointScript(mappedX, mappedY));
        }

        public static void SynthesizeClick(IWebViewBackend backend, Vector2 localPosition)
        {
            SynthesizeClick(backend, Mathf.RoundToInt(localPosition.x), Mathf.RoundToInt(localPosition.y));
        }
    }
}

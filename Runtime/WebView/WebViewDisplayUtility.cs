using UnityEngine;
using UnityEngine.UI;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Helpers for displaying a WebView texture in Unity UI (gree bitmap is bottom-up).
    /// </summary>
    public static class WebViewDisplayUtility
    {
        public static void ApplyToRawImage(RawImage target, IWebViewBackend backend)
        {
            if (target == null || backend == null)
                return;

            target.texture = backend.Texture;
            target.uvRect = backend.FlipVertically
                ? new Rect(0f, 1f, 1f, -1f)
                : new Rect(0f, 0f, 1f, 1f);
        }
    }
}

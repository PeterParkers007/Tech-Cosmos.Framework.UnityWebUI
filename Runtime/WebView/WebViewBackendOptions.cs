namespace UnityWebUI.WebView
{
    public sealed class WebViewBackendOptions
    {
        public static WebViewBackendOptions Default { get; } = new WebViewBackendOptions();

        public WebViewPumpPriority Priority { get; set; } = WebViewPumpPriority.Runtime;
        public int BitmapRefreshCycle { get; set; } = 1;
        public float RenderScale { get; set; } = 1f;
        public bool TransparentBackground { get; set; }
    }
}

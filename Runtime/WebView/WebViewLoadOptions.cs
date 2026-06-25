namespace UnityWebUI.WebView
{
    public sealed class WebViewLoadOptions
    {
        public string BaseDirectory { get; set; }
        public bool AllowRemoteNavigation { get; set; }
        public bool InjectBridgeScript { get; set; } = true;
    }
}

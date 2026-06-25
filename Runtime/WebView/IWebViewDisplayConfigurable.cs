namespace UnityWebUI.WebView
{
    public interface IWebViewDisplayConfigurable
    {
        void ConfigureDisplay(int bitmapRefreshCycle, WebViewPumpPriority priority, float renderScale = 1f);
    }
}

namespace UnityWebUI.WebView
{
    public interface IWebViewPumpTarget
    {
        WebViewPumpPriority PumpPriority { get; }
        int RefreshCycle { get; }
        void Pump(bool captureFrame);
        void SyncDisplay();
    }
}

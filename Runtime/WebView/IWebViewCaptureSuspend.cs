namespace UnityWebUI.WebView
{
    /// <summary>
    /// Pause bitmap/GPU capture while keeping the WebView instance alive (ESC menu, overlay toggle).
    /// </summary>
    public interface IWebViewCaptureSuspend
    {
        bool IsCaptureSuspended { get; }

        void SetCaptureSuspended(bool suspended);
    }
}

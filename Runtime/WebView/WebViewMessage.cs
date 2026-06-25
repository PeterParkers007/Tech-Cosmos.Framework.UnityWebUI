using System;

namespace UnityWebUI.WebView
{
    [Serializable]
    public sealed class WebViewMessage
    {
        public string type;
        public string id;
        public string href;
    }
}

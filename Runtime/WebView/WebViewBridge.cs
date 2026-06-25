using System;
using UnityEngine;

namespace UnityWebUI.WebView
{
    public sealed class WebViewBridge
    {
        public event Action<string> ActionClicked;
        public event Action<string> NavigateRequested;
        public event Action<WebViewMessage> MessageReceived;

        public void HandleRawMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            WebViewMessage message;
            try
            {
                message = JsonUtility.FromJson<WebViewMessage>(json.Trim());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"UnityWebUI: invalid bridge message: {json} ({ex.Message})");
                return;
            }

            if (message == null || string.IsNullOrEmpty(message.type))
                return;

            MessageReceived?.Invoke(message);

            switch (message.type)
            {
                case "action":
                    if (!string.IsNullOrEmpty(message.id))
                        ActionClicked?.Invoke(message.id);
                    break;
                case "navigate":
                    if (!string.IsNullOrEmpty(message.href))
                        NavigateRequested?.Invoke(message.href);
                    break;
            }
        }
    }
}

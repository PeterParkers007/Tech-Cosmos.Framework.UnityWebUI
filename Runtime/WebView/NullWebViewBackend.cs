using System;
using UnityEngine;

namespace UnityWebUI.WebView
{
    public sealed class NullWebViewBackend : IWebViewBackend
    {
        public bool IsAvailable => false;
        public string StatusMessage { get; }
        public Texture Texture => null;
        public bool FlipVertically => false;

        public event Action<string> MessageEmitted;
        public event Action<string> UrlChanged;
        public event Action Initialized;

        public NullWebViewBackend(string statusMessage)
        {
            StatusMessage = statusMessage;
        }

        public void Attach(Transform hostTransform) { }
        public void SetSize(int width, int height) { }
        public void LoadUrl(string url) => Debug.LogWarning($"UnityWebUI: {StatusMessage}");
        public void PostMessage(string message) { }
        public void Click(int x, int y) { }
        public void PointerDown(int x, int y) { }
        public void PointerUp(int x, int y) { }
        public void MovePointer(int x, int y) { }
        public void LeavePointer() { }
        public void Scroll(int deltaX, int deltaY) { }
        public void Tick() { }
        public void Dispose() { }
    }
}

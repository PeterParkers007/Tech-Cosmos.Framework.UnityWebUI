using System;
using UnityEngine;

namespace UnityWebUI.WebView
{
    public interface IWebViewBackend : IDisposable
    {
        bool IsAvailable { get; }
        string StatusMessage { get; }
        Texture Texture { get; }
        bool FlipVertically { get; }

        event Action<string> MessageEmitted;
        event Action<string> UrlChanged;
        event Action Initialized;

        void Attach(Transform hostTransform);
        void SetSize(int width, int height);
        void LoadUrl(string url);
        void PostMessage(string message);
        void Click(int x, int y);
        void PointerDown(int x, int y);
        void PointerUp(int x, int y);
        void MovePointer(int x, int y);
        void LeavePointer();
        void Scroll(int deltaX, int deltaY);
        void Tick();
    }
}

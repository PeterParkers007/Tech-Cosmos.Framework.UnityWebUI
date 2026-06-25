using System;
using UnityEngine;

namespace UnityWebUI.WebView
{
    public sealed class WebViewSession : IDisposable
    {
        readonly GameObject _hostObject;
        readonly WebViewBridge _bridge = new WebViewBridge();

        public WebViewBridge Bridge => _bridge;
        public IWebViewBackend Backend { get; }
        public string LoadedFilePath { get; private set; }
        public WebViewLoadOptions Options { get; } = new WebViewLoadOptions();

        public WebViewSession(WebViewBackendOptions backendOptions = null)
        {
            _hostObject = new GameObject("UnityWebUI.WebViewSession")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Backend = WebViewBackendFactory.Create(_hostObject.transform, backendOptions);
            Backend.MessageEmitted += _bridge.HandleRawMessage;
            Backend.Initialized += OnBackendInitialized;
        }

        void OnBackendInitialized()
        {
            if (!string.IsNullOrEmpty(LoadedFilePath))
                Backend?.LoadUrl(LocalPageUrl.ToFileUrl(LoadedFilePath));
        }

        public void SetSize(int width, int height)
        {
            Backend?.SetSize(Mathf.Max(1, width), Mathf.Max(1, height));
        }

        public void LoadLocalHtml(string htmlFilePath)
        {
            if (Backend == null)
                return;

            var resolved = LocalPageUrl.ResolveHtmlPath(htmlFilePath, Options.BaseDirectory);
            LoadedFilePath = resolved;
            Options.BaseDirectory = System.IO.Path.GetDirectoryName(resolved);
            Backend.LoadUrl(LocalPageUrl.ToFileUrl(resolved));
        }

        public void Tick()
        {
            WebViewPerformanceHub.TickAll(Time.frameCount);
        }

        public void Dispose()
        {
            if (Backend != null)
            {
                Backend.MessageEmitted -= _bridge.HandleRawMessage;
                Backend.Initialized -= OnBackendInitialized;
            }

            Backend?.Dispose();

            if (_hostObject != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_hostObject);
                else
                    UnityEngine.Object.DestroyImmediate(_hostObject);
            }
        }
    }
}

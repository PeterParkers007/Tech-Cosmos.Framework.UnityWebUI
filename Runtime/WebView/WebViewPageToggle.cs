using UnityEngine;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Toggle WebViewHost page visibility (e.g. ESC menu). Keeps WebView warm; stops capture while hidden.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WebViewPageToggle : MonoBehaviour
    {
        [SerializeField] WebViewHost _host;
        [SerializeField] KeyCode _toggleKey = KeyCode.Escape;
        [SerializeField] bool _toggleOnKeyDown = true;

        public WebViewHost Host
        {
            get => _host;
            set => _host = value;
        }

        void Reset()
        {
            _host = GetComponent<WebViewHost>();
        }

        void Awake()
        {
            if (_host == null)
                _host = GetComponent<WebViewHost>();
        }

        void Update()
        {
            if (_host == null || !_toggleOnKeyDown || !Input.GetKeyDown(_toggleKey))
                return;

            Toggle();
        }

        public void Toggle()
        {
            if (_host == null)
                return;

            _host.SetPageVisible(!_host.IsPageVisible);
        }

        public void Show() => _host?.SetPageVisible(true);

        public void Hide() => _host?.SetPageVisible(false);
    }
}

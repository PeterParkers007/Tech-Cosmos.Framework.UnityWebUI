using System;
using UnityEngine;
using UnityEngine.Events;

namespace UnityWebUI.WebView
{
    [Serializable]
    public sealed class WebViewActionBindingEntry
    {
        [SerializeField] string _actionId = string.Empty;
        [SerializeField] string _displayName = string.Empty;
        [SerializeField] UnityEvent _onInvoked = new UnityEvent();

        public string ActionId => _actionId;
        public string DisplayName => _displayName;
        public UnityEvent OnInvoked => _onInvoked;

        public void SetActionId(string actionId)
        {
            _actionId = actionId ?? string.Empty;
        }

        public void SetDisplayName(string displayName)
        {
            _displayName = displayName ?? string.Empty;
        }

        public bool HasListeners =>
            _onInvoked != null && _onInvoked.GetPersistentEventCount() > 0;
    }
}

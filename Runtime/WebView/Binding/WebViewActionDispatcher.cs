using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Play Mode: forwards WebViewBridge action ids to UnityEvents on this scene component.
    /// UnityEvents live here (not on the ScriptableObject profile) so Hierarchy objects can be assigned.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WebViewHost))]
    [AddComponentMenu("Unity Web UI/WebView Action Dispatcher")]
    public sealed class WebViewActionDispatcher : MonoBehaviour
    {
        [SerializeField] WebViewHost _host;
        [SerializeField] WebUIViewBindingProfile _profile;
        [SerializeField] List<WebViewActionBindingEntry> _bindings = new List<WebViewActionBindingEntry>();

        public WebUIViewBindingProfile Profile
        {
            get => _profile;
            set => _profile = value;
        }

        public IReadOnlyList<WebViewActionBindingEntry> Bindings => _bindings;

        void Reset()
        {
            _host = GetComponent<WebViewHost>();
        }

        void Awake()
        {
            if (_host == null)
                _host = GetComponent<WebViewHost>();

            if (_profile == null && _host != null)
                _profile = _host.BindingProfile;
        }

        void OnEnable()
        {
            if (_host == null)
                _host = GetComponent<WebViewHost>();

            if (_host != null)
                _host.Bridge.ActionClicked += OnActionClicked;
        }

        void OnDisable()
        {
            if (_host != null)
                _host.Bridge.ActionClicked -= OnActionClicked;
        }

        public WebViewActionBindingEntry FindOrCreate(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return null;

            for (var i = 0; i < _bindings.Count; i++)
            {
                if (string.Equals(_bindings[i].ActionId, actionId, StringComparison.Ordinal))
                    return _bindings[i];
            }

            var entry = new WebViewActionBindingEntry();
            entry.SetActionId(actionId);
            _bindings.Add(entry);
            return entry;
        }

        public int IndexOf(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return -1;

            for (var i = 0; i < _bindings.Count; i++)
            {
                if (string.Equals(_bindings[i].ActionId, actionId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public bool TryInvoke(string actionId)
        {
            var index = IndexOf(actionId);
            if (index < 0)
                return false;

            _bindings[index].OnInvoked?.Invoke();
            return true;
        }

        public void SyncWithScannedActions(IReadOnlyList<HtmlActionScanner.ScannedAction> scannedActions)
        {
            if (scannedActions == null)
                return;

            for (var i = 0; i < scannedActions.Count; i++)
            {
                var scanned = scannedActions[i];
                var entry = FindOrCreate(scanned.Id);
                if (entry == null)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.DisplayName) && !string.IsNullOrWhiteSpace(scanned.TextHint))
                    entry.SetDisplayName(scanned.TextHint);
            }
        }

        public void RemoveOrphans(ISet<string> validActionIds)
        {
            if (validActionIds == null)
                return;

            for (var i = _bindings.Count - 1; i >= 0; i--)
            {
                var id = _bindings[i].ActionId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    _bindings.RemoveAt(i);
                    continue;
                }

                if (!validActionIds.Contains(id))
                    _bindings.RemoveAt(i);
            }
        }

        void OnActionClicked(string actionId)
        {
            if (TryInvoke(actionId))
                return;

            if (_profile != null && _profile.TryInvoke(actionId))
                return;

            Debug.LogWarning(
                _profile != null
                    ? $"UnityWebUI: action '{actionId}' has no listener on '{name}' (WebViewActionDispatcher) or profile '{_profile.name}'."
                    : $"UnityWebUI: action '{actionId}' has no matching binding on '{name}' (WebViewActionDispatcher).",
                this);
        }
    }
}

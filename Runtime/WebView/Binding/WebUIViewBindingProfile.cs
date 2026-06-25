using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityWebUI.WebView
{
    [CreateAssetMenu(
        fileName = "WebUIViewBindingProfile",
        menuName = "Unity Web UI/View Binding Profile")]
    public sealed class WebUIViewBindingProfile : ScriptableObject
    {
        [SerializeField] string _sourceHtmlPath = string.Empty;
        [SerializeField] List<WebViewActionBindingEntry> _bindings = new List<WebViewActionBindingEntry>();

        public string SourceHtmlPath => _sourceHtmlPath;
        public IReadOnlyList<WebViewActionBindingEntry> Bindings => _bindings;

        public void SetSourceHtmlPath(string htmlPath)
        {
            _sourceHtmlPath = NormalizeHtmlPath(htmlPath);
        }

        public bool MatchesHtmlPath(string htmlPath)
        {
            if (string.IsNullOrWhiteSpace(htmlPath))
                return string.IsNullOrWhiteSpace(_sourceHtmlPath);

            return string.Equals(
                NormalizeHtmlPath(htmlPath),
                NormalizeHtmlPath(_sourceHtmlPath),
                StringComparison.OrdinalIgnoreCase);
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

        public static string NormalizeHtmlPath(string htmlPath)
        {
            if (string.IsNullOrWhiteSpace(htmlPath))
                return string.Empty;

            try
            {
                return System.IO.Path.GetFullPath(htmlPath.Trim());
            }
            catch
            {
                return htmlPath.Trim();
            }
        }
    }
}

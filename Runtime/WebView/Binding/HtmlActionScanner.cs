using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityWebUI.WebView
{
    public static class HtmlActionScanner
    {
        public readonly struct ScannedAction
        {
            public ScannedAction(string id, string tagHint, string textHint, bool isButton)
            {
                Id = id ?? string.Empty;
                TagHint = tagHint ?? string.Empty;
                TextHint = textHint ?? string.Empty;
                IsButton = isButton;
            }

            public string Id { get; }
            public string TagHint { get; }
            public string TextHint { get; }
            public bool IsButton { get; }
        }

        static readonly Regex ActionAttributeRegex = new Regex(
            @"data-unity-action\s*=\s*(?:""([^""]+)""|'([^']+)')",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        static readonly Regex TagRegex = new Regex(
            @"<(a|input|div|span)[^>]*data-unity-action\s*=\s*(?:""([^""]+)""|'([^']+)')[^>]*>(.*?)</\1>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        static readonly Regex ButtonRegex = new Regex(
            @"<button\b([^>]*)>(.*?)</button>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        static readonly Regex AttributeRegex = new Regex(
            @"(data-unity-action|id)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'>]+))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IReadOnlyList<ScannedAction> ScanFile(string htmlFilePath)
        {
            if (string.IsNullOrWhiteSpace(htmlFilePath) || !System.IO.File.Exists(htmlFilePath))
                return Array.Empty<ScannedAction>();

            try
            {
                return ScanText(System.IO.File.ReadAllText(htmlFilePath));
            }
            catch
            {
                return Array.Empty<ScannedAction>();
            }
        }

        public static IReadOnlyList<ScannedAction> ScanText(string html)
        {
            if (string.IsNullOrEmpty(html))
                return Array.Empty<ScannedAction>();

            var byId = new Dictionary<string, ScannedAction>(StringComparer.Ordinal);
            ScanButtons(html, byId);
            ScanTaggedActions(html, byId);
            ScanBareActionAttributes(html, byId);

            var list = new List<ScannedAction>(byId.Values);
            list.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.Ordinal));
            return list;
        }

        static void ScanButtons(string html, Dictionary<string, ScannedAction> byId)
        {
            var matches = ButtonRegex.Matches(html);
            if (matches.Count == 0)
                return;

            var descriptors = new List<WebViewButtonActionResolver.ButtonDescriptor>(matches.Count);
            var textHints = new List<string>(matches.Count);

            foreach (Match match in matches)
            {
                ParseAttributes(match.Groups[1].Value, out var htmlId, out var dataUnityAction);
                var text = StripTags(match.Groups[2].Value).Trim();
                if (text.Length > 48)
                    text = text.Substring(0, 48) + "...";

                descriptors.Add(new WebViewButtonActionResolver.ButtonDescriptor(htmlId, dataUnityAction, text));
                textHints.Add(text);
            }

            var ids = WebViewButtonActionResolver.ResolveAll(descriptors);
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                byId[id] = new ScannedAction(id, "button", textHints[i], isButton: true);
            }
        }

        static void ScanTaggedActions(string html, Dictionary<string, ScannedAction> byId)
        {
            foreach (Match match in TagRegex.Matches(html))
            {
                var id = !string.IsNullOrEmpty(match.Groups[2].Value)
                    ? match.Groups[2].Value
                    : match.Groups[3].Value;
                if (string.IsNullOrWhiteSpace(id) || byId.ContainsKey(id))
                    continue;

                var tag = match.Groups[1].Value;
                var text = StripTags(match.Groups[4].Value).Trim();
                if (text.Length > 48)
                    text = text.Substring(0, 48) + "...";

                byId[id] = new ScannedAction(id, tag, text, isButton: false);
            }
        }

        static void ScanBareActionAttributes(string html, Dictionary<string, ScannedAction> byId)
        {
            foreach (Match match in ActionAttributeRegex.Matches(html))
            {
                var id = !string.IsNullOrEmpty(match.Groups[1].Value)
                    ? match.Groups[1].Value
                    : match.Groups[2].Value;
                if (string.IsNullOrWhiteSpace(id) || byId.ContainsKey(id))
                    continue;

                byId[id] = new ScannedAction(id, string.Empty, string.Empty, isButton: false);
            }
        }

        static void ParseAttributes(string attributeText, out string htmlId, out string dataUnityAction)
        {
            htmlId = string.Empty;
            dataUnityAction = string.Empty;

            foreach (Match match in AttributeRegex.Matches(attributeText))
            {
                var name = match.Groups[1].Value;
                var value = FirstNonEmpty(match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (name.Equals("id", StringComparison.OrdinalIgnoreCase))
                    htmlId = value.Trim();
                else if (name.Equals("data-unity-action", StringComparison.OrdinalIgnoreCase))
                    dataUnityAction = value.Trim();
            }
        }

        static string FirstNonEmpty(params string[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }

        static string StripTags(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return Regex.Replace(value, "<.*?>", " ");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Stable action IDs for HTML buttons. Must stay in sync with unity-bridge.js.
    /// </summary>
    public static class WebViewButtonActionResolver
    {
        public readonly struct ButtonDescriptor
        {
            public ButtonDescriptor(string htmlId, string dataUnityAction, string textContent)
            {
                HtmlId = htmlId ?? string.Empty;
                DataUnityAction = dataUnityAction ?? string.Empty;
                TextContent = textContent ?? string.Empty;
            }

            public string HtmlId { get; }
            public string DataUnityAction { get; }
            public string TextContent { get; }
        }

        public static IReadOnlyList<string> ResolveAll(IReadOnlyList<ButtonDescriptor> buttons)
        {
            if (buttons == null || buttons.Count == 0)
                return Array.Empty<string>();

            var results = new string[buttons.Count];
            var slugCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var i = 0; i < buttons.Count; i++)
            {
                var button = buttons[i];
                if (!string.IsNullOrWhiteSpace(button.DataUnityAction))
                {
                    results[i] = button.DataUnityAction.Trim();
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(button.HtmlId))
                {
                    results[i] = button.HtmlId.Trim();
                    continue;
                }

                var slug = SlugFromText(button.TextContent);
                if (!string.IsNullOrEmpty(slug))
                {
                    if (!slugCounts.TryGetValue(slug, out var count))
                    {
                        slugCounts[slug] = 1;
                        results[i] = slug;
                    }
                    else
                    {
                        count++;
                        slugCounts[slug] = count;
                        results[i] = slug + "-" + count;
                    }

                    continue;
                }

                results[i] = "button-" + i;
            }

            return results;
        }

        public static string SlugFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = StripTags(text).Trim();
            if (text.Length == 0)
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            var lastWasSeparator = false;
            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch) || IsCjk(ch))
                {
                    sb.Append(ch);
                    lastWasSeparator = false;
                    continue;
                }

                if (!lastWasSeparator && sb.Length > 0)
                {
                    sb.Append('-');
                    lastWasSeparator = true;
                }
            }

            var slug = sb.ToString().Trim('-');
            if (slug.Length > 32)
                slug = slug.Substring(0, 32).Trim('-');

            return slug;
        }

        static bool IsCjk(char ch) => ch >= '\u4e00' && ch <= '\u9fff';

        static string StripTags(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return System.Text.RegularExpressions.Regex.Replace(value, "<.*?>", " ");
        }
    }
}

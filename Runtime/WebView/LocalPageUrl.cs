using System;
using System.IO;

namespace UnityWebUI.WebView
{
    public static class LocalPageUrl
    {
        public static string ToFileUrl(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("HTML file path is required.", nameof(filePath));

            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("HTML file not found.", fullPath);

            return new Uri(fullPath).AbsoluteUri;
        }

        public static string ResolveHtmlPath(string htmlPath, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(htmlPath))
                return null;

            if (Path.IsPathRooted(htmlPath))
                return Path.GetFullPath(htmlPath);

            if (string.IsNullOrWhiteSpace(baseDirectory))
                return Path.GetFullPath(htmlPath);

            return Path.GetFullPath(Path.Combine(baseDirectory, htmlPath));
        }
    }
}

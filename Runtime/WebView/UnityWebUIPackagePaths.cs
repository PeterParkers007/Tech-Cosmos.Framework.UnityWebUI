using System.IO;
using System.Reflection;
using UnityEngine;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Resolves bundled package files (UPM under Packages/ or embedded under Assets/).
    /// </summary>
    public static class UnityWebUIPackagePaths
    {
        public const string PackageName = "com.unitywebui.core";
        public const string BridgeResourcePath = "UnityWebUI/unity-bridge";

        public static string TryGetPackageRoot()
        {
#if UNITY_EDITOR
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UnityWebUIPackagePaths).Assembly);
            if (info != null && !string.IsNullOrEmpty(info.resolvedPath))
                return info.resolvedPath;
#endif
            return TryGetPackageRootFromAssembly();
        }

        static string TryGetPackageRootFromAssembly()
        {
            var assemblyPath = typeof(UnityWebUIPackagePaths).Assembly.Location;
            if (string.IsNullOrEmpty(assemblyPath))
                return null;

            var dir = new DirectoryInfo(Path.GetDirectoryName(assemblyPath));
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "package.json")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return null;
        }

        public static string GetBundledStreamingAssetPath(string relativePath)
        {
            var root = TryGetPackageRoot();
            if (string.IsNullOrEmpty(root))
                return null;

            return Path.Combine(root, "StreamingAssets", relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string GetProjectStreamingAssetPath(string relativePath)
        {
            return Path.Combine(Application.streamingAssetsPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string GetGpuPluginDllPath()
        {
            var root = TryGetPackageRoot();
            if (!string.IsNullOrEmpty(root))
            {
                return Path.GetFullPath(Path.Combine(
                    root, "Plugins", "Windows", "x86_64", "UnityWebUI.WebView2Gpu.dll"));
            }

            return Path.GetFullPath(Path.Combine(
                Application.dataPath, "UnityWebUI", "Plugins", "Windows", "x86_64", "UnityWebUI.WebView2Gpu.dll"));
        }

        public static string GetPackageBuiltGpuDllPath()
        {
            var root = TryGetPackageRoot();
            if (string.IsNullOrEmpty(root))
                return null;

            return Path.GetFullPath(Path.Combine(
                root, "Native", "Windows", "UnityWebUI.WebView2Gpu", "bin", "x64", "Release", "UnityWebUI.WebView2Gpu.dll"));
        }

        public static string GetProjectWebView2BuildGpuDllPath()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "WebView2Build", "Native", "x64", "Release", "_out", "UnityWebUI.WebView2Gpu.dll"));
        }

#if UNITY_EDITOR
        public static string TryGetAssetsRelativePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return null;

            var assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalized = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!normalized.StartsWith(assetsRoot, System.StringComparison.OrdinalIgnoreCase))
                return null;

            return ("Assets" + normalized.Substring(assetsRoot.Length)).Replace('\\', '/');
        }

        public static string GetDefaultBindingProfilesFolder()
        {
            var root = TryGetPackageRoot();
            if (!string.IsNullOrEmpty(root))
            {
                var assetsRelative = TryGetAssetsRelativePath(Path.Combine(root, "BindingProfiles"));
                if (!string.IsNullOrEmpty(assetsRelative))
                    return assetsRelative;
            }

            return "Assets/UnityWebUI/BindingProfiles";
        }
#endif
    }
}

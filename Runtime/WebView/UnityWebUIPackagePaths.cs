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
    }
}

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityWebUI.Editor
{
    static class UnityWebUIEditorPackagePaths
    {
        public static string PackageRoot
        {
            get
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UnityWebUIEditorPackagePaths).Assembly);
                if (info != null && !string.IsNullOrEmpty(info.resolvedPath))
                    return info.resolvedPath;

                return Path.GetFullPath(Path.Combine(Application.dataPath, "UnityWebUI"));
            }
        }

        public static string PluginDllPath =>
            Path.Combine(PackageRoot, "Plugins", "Windows", "x86_64", "UnityWebUI.WebView2Gpu.dll");

        public static string PendingDllPath =>
            Path.Combine(PackageRoot, "Plugins", "Windows", "x86_64", "UnityWebUI.WebView2Gpu.dll.pending");

        public static string BuiltDllPath =>
            Path.Combine(PackageRoot, "Native", "Windows", "UnityWebUI.WebView2Gpu", "bin", "x64", "Release", "UnityWebUI.WebView2Gpu.dll");
    }
}
#endif

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    static class UnityWebUIEditorPackagePaths
    {
        public static string PackageRoot
        {
            get
            {
                var root = UnityWebUIPackagePaths.TryGetPackageRoot();
                if (!string.IsNullOrEmpty(root))
                    return root;

                return Path.GetFullPath(Path.Combine(Application.dataPath, "UnityWebUI"));
            }
        }

        public static string NativeWindowsPath => Path.Combine(PackageRoot, "Native", "Windows");

        public static string BuildBatPath => Path.Combine(NativeWindowsPath, "build.bat");

        public static string ApplyGpuPluginBatPath => Path.Combine(NativeWindowsPath, "apply-gpu-plugin.bat");

        public static string BuildLogPath => Path.Combine(NativeWindowsPath, "build-last.log");

        public static string PluginDllPath =>
            Path.Combine(PackageRoot, "Plugins", "Windows", "x86_64", "UnityWebUI.WebView2Gpu.dll");

        public static string PendingDllPath =>
            Path.Combine(PackageRoot, "Plugins", "Windows", "x86_64", "UnityWebUI.WebView2Gpu.dll.pending");

        public static string BuiltDllPath => UnityWebUIPackagePaths.GetPackageBuiltGpuDllPath()
            ?? Path.Combine(PackageRoot, "Native", "Windows", "UnityWebUI.WebView2Gpu", "bin", "x64", "Release", "UnityWebUI.WebView2Gpu.dll");
    }
}
#endif

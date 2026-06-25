#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityWebUI.Editor
{
    /// <summary>
    /// Copies the freshly built GPU plugin from WebView2Build into Plugins.
    /// Runs on domain reload so the DLL is not locked by a previous native load.
    /// </summary>
    [InitializeOnLoad]
    static class WebViewGpuPluginDeploy
    {
        const string PendingFileName = "UnityWebUI.WebView2Gpu.dll.pending";

        static WebViewGpuPluginDeploy()
        {
            TryDeploy(out _);
            EditorApplication.quitting += OnEditorQuitting;
        }

        static void OnEditorQuitting()
        {
            if (!HasPendingUpdate && !IsPluginStale())
                return;

            if (TryDeploy(out var message))
                Debug.Log("[UnityWebUI] GPU plugin applied on editor exit. Next launch will use the new native DLL.\n" + message);
        }

        public static string BuiltDllPath => UnityWebUIEditorPackagePaths.BuiltDllPath;

        public static string PluginDllPath => UnityWebUIEditorPackagePaths.PluginDllPath;

        public static string PendingDllPath => UnityWebUIEditorPackagePaths.PendingDllPath;

        public static bool HasPendingUpdate => File.Exists(PendingDllPath);

        public static bool IsPluginStale()
        {
            if (!File.Exists(PluginDllPath))
                return true;

            var source = ResolveSourceDll();
            if (source == null)
                return false;

            return File.GetLastWriteTimeUtc(source) > File.GetLastWriteTimeUtc(PluginDllPath);
        }

        public static void ClosePreviewWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<WebViewPreviewWindow>();
            foreach (var window in windows)
            {
                if (window != null)
                    window.Close();
            }
        }

        [MenuItem("Window/Unity Web UI/Apply GPU Plugin Update")]
        public static void ApplyFromMenu()
        {
            ClosePreviewWindows();
            if (TryDeploy(out var message))
            {
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "Unity Web UI",
                    message + "\n\nRestart Unity Editor now so the native plugin reloads (script reload is not enough).",
                    "OK");
                return;
            }

            EditorUtility.RequestScriptReload();
            EditorUtility.DisplayDialog(
                "Unity Web UI",
                message + "\n\nReloading scripts now — reopen WebView Preview after reload finishes.",
                "OK");
        }

        public static bool TryDeploy(out string message)
        {
            message = null;
            var source = ResolveSourceDll();
            if (source == null)
            {
                message = "No built GPU plugin found. Run Build Windows GPU Plugin first.";
                return false;
            }

            var pluginDir = Path.GetDirectoryName(PluginDllPath);
            if (!Directory.Exists(pluginDir))
                Directory.CreateDirectory(pluginDir);

            try
            {
                File.Copy(source, PluginDllPath, overwrite: true);
                if (File.Exists(PendingDllPath))
                    File.Delete(PendingDllPath);
            }
            catch (IOException ex)
            {
                message = "Plugins DLL is locked by Unity.\n\n" +
                            "1. Close Unity completely\n" +
                            "2. Run: Assets/UnityWebUI/Native/Windows/apply-gpu-plugin.bat\n" +
                            "3. Reopen the project\n\n" + ex.Message;
                try
                {
                    File.Copy(source, PendingDllPath, overwrite: true);
                }
                catch
                {
                    // ignored
                }
                return false;
            }

            var builtTime = File.GetLastWriteTimeUtc(source);
            var pluginTime = File.GetLastWriteTimeUtc(PluginDllPath);
            message = builtTime <= pluginTime
                ? "GPU plugin updated in Plugins/Windows/x86_64."
                : "GPU plugin copied to Plugins/Windows/x86_64.";
            return true;
        }

        static string ResolveSourceDll()
        {
            if (File.Exists(BuiltDllPath))
                return BuiltDllPath;
            if (File.Exists(PendingDllPath))
                return PendingDllPath;
            return null;
        }
    }
}
#endif

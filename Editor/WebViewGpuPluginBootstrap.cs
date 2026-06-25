#if UNITY_EDITOR
using UnityEditor;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    /// <summary>
    /// Deploy fresh GPU plugin; native DLL is preloaded so UnityPluginLoad runs at startup.
    /// </summary>
    [InitializeOnLoad]
    static class WebViewGpuPluginBootstrap
    {
        static WebViewGpuPluginBootstrap()
        {
            var deployed = WebViewGpuPluginDeploy.TryDeploy(out var deployMessage);
            if (!deployed)
            {
                if (WebViewGpuPluginDeploy.HasPendingUpdate || WebViewGpuPluginDeploy.IsPluginStale())
                {
                    UnityEngine.Debug.LogWarning(
                        "[UnityWebUI] GPU plugin on disk is outdated and could not be replaced while Unity is running.\n" +
                        "Close Unity, run apply-gpu-plugin.bat in:\n" + UnityWebUIEditorPackagePaths.NativeWindowsPath + "\n" +
                        "then reopen the project.\n" +
                        deployMessage);
                }
                return;
            }

            if (!WindowsGpuWebViewNative.IsDllPresent())
                return;

            UnityEngine.Debug.Log("[UnityWebUI] GPU plugin present. Diagnostics run after entering Play Mode.");
        }
    }
}
#endif

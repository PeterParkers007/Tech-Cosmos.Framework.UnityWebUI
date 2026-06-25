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
                        "Close Unity, run Assets/UnityWebUI/Native/Windows/apply-gpu-plugin.bat, then reopen the project.\n" +
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

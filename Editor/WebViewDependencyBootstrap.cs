#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace UnityWebUI.Editor
{
    /// <summary>
    /// Logs once if the bundled WebView UPM dependency is missing.
    /// </summary>
    [InitializeOnLoad]
    static class WebViewDependencyBootstrap
    {
        const string GreePackageId = "net.gree.unity-webview";

        static WebViewDependencyBootstrap()
        {
            EditorApplication.delayCall += CheckDependency;
        }

        static void CheckDependency()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            var request = Client.List(true, false);
            while (!request.IsCompleted)
            { }

            if (request.Status != StatusCode.Success)
                return;

            foreach (var package in request.Result)
            {
                if (package.name == GreePackageId)
                    return;
            }

            Debug.LogWarning(
                "UnityWebUI: 缺少 WebView 依赖 net.gree.unity-webview（gree 回退后端）。\n" +
                "Windows GPU 主后端：菜单 Window → Unity Web UI → Build Windows GPU Plugin\n" +
                "或在 Packages/manifest.json 加入 gree UPM 作为回退。");
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace UnityWebUI.Editor
{
    /// <summary>
    /// Logs once if the optional gree fallback package is missing.
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

            if (UnityWebUIProjectSetup.IsPackageInstalled(GreePackageId))
                return;

            if (UnityWebUIProjectSetup.IsProjectReady(out _))
                return;

            Debug.LogWarning(
                "UnityWebUI: GPU 主路径未就绪，且未安装可选回退 net.gree.unity-webview。\n" +
                "菜单 Window → Unity Web UI → Setup Project");
        }
    }
}
#endif

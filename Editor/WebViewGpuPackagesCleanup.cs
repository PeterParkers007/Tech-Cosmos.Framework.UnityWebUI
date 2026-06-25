#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityWebUI.Editor
{
    /// <summary>
    /// NuGet packages must not live under Assets. One-time cleanup of the legacy folder.
    /// </summary>
    static class WebViewGpuPackagesCleanup
    {
        const string LegacyPackagesPath = "Assets/UnityWebUI/Native/Windows/packages";
        const string PrefKey = "UnityWebUI.WebViewGpu.LegacyPackagesRemoved";

        [InitializeOnLoadMethod]
        static void RemoveLegacyPackagesOnce()
        {
            if (EditorPrefs.GetBool(PrefKey, false))
                return;

            EditorApplication.delayCall += () =>
            {
                if (!AssetDatabase.IsValidFolder(LegacyPackagesPath))
                {
                    EditorPrefs.SetBool(PrefKey, true);
                    return;
                }

                if (AssetDatabase.DeleteAsset(LegacyPackagesPath))
                    Debug.Log("UnityWebUI: Removed legacy NuGet packages from Assets (moved to Project/WebView2Build/).");

                var metaPath = LegacyPackagesPath + ".meta";
                if (File.Exists(metaPath))
                    AssetDatabase.DeleteAsset(metaPath);

                EditorPrefs.SetBool(PrefKey, true);
                AssetDatabase.Refresh();
            };
        }
    }
}
#endif

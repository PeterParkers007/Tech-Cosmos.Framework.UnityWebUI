#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    /// <summary>
    /// NuGet packages must not live under Assets. One-time cleanup of the legacy folder.
    /// </summary>
    static class WebViewGpuPackagesCleanup
    {
        const string PrefKey = "UnityWebUI.WebViewGpu.LegacyPackagesRemoved";

        [InitializeOnLoadMethod]
        static void RemoveLegacyPackagesOnce()
        {
            if (EditorPrefs.GetBool(PrefKey, false))
                return;

            EditorApplication.delayCall += () =>
            {
                var removed = false;
                foreach (var legacyPackagesDir in GetLegacyPackagesDirectories())
                {
                    if (!Directory.Exists(legacyPackagesDir))
                        continue;

                    var assetsRelative = UnityWebUIPackagePaths.TryGetAssetsRelativePath(legacyPackagesDir);
                    if (!string.IsNullOrEmpty(assetsRelative) && AssetDatabase.IsValidFolder(assetsRelative))
                    {
                        if (AssetDatabase.DeleteAsset(assetsRelative))
                            removed = true;

                        var metaPath = assetsRelative + ".meta";
                        if (File.Exists(metaPath))
                            AssetDatabase.DeleteAsset(metaPath);
                        continue;
                    }

                    try
                    {
                        Directory.Delete(legacyPackagesDir, recursive: true);
                        removed = true;
                        var metaPath = legacyPackagesDir + ".meta";
                        if (File.Exists(metaPath))
                            File.Delete(metaPath);
                    }
                    catch (IOException ex)
                    {
                        Debug.LogWarning("UnityWebUI: Could not remove legacy packages folder: " + ex.Message);
                    }
                }

                if (removed)
                    Debug.Log("UnityWebUI: Removed legacy NuGet packages from Assets (moved to Project/WebView2Build/).");

                EditorPrefs.SetBool(PrefKey, true);
                AssetDatabase.Refresh();
            };
        }

        static string[] GetLegacyPackagesDirectories()
        {
            return new[]
            {
                Path.Combine(UnityWebUIEditorPackagePaths.NativeWindowsPath, "packages"),
                Path.Combine(Application.dataPath, "UnityWebUI", "Native", "Windows", "packages"),
            };
        }
    }
}
#endif

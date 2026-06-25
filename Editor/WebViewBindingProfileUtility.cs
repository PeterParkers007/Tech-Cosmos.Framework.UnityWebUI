#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    static class WebViewBindingProfileUtility
    {
        const string DefaultFolder = "Assets/UnityWebUI/BindingProfiles";

        public static WebUIViewBindingProfile GetOrCreateProfileForHtml(string htmlFullPath)
        {
            var normalized = WebUIViewBindingProfile.NormalizeHtmlPath(htmlFullPath);
            if (string.IsNullOrEmpty(normalized))
                return null;

            var existing = FindProfileByHtmlPath(normalized);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder(DefaultFolder))
            {
                Directory.CreateDirectory(DefaultFolder);
                AssetDatabase.Refresh();
            }

            var baseName = Path.GetFileNameWithoutExtension(normalized);
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "WebViewBindings";

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{DefaultFolder}/{baseName}.asset");
            var profile = ScriptableObject.CreateInstance<WebUIViewBindingProfile>();
            profile.SetSourceHtmlPath(normalized);
            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        public static WebUIViewBindingProfile FindProfileByHtmlPath(string htmlFullPath)
        {
            var normalized = WebUIViewBindingProfile.NormalizeHtmlPath(htmlFullPath);
            if (string.IsNullOrEmpty(normalized))
                return null;

            foreach (var guid in AssetDatabase.FindAssets("t:WebUIViewBindingProfile"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<WebUIViewBindingProfile>(path);
                if (profile != null && profile.MatchesHtmlPath(normalized))
                    return profile;
            }

            return null;
        }

        public static void LinkHostToProfile(WebViewHost host, WebUIViewBindingProfile profile)
        {
            if (host == null || profile == null)
                return;

            Undo.RecordObject(host, "Link WebView Binding Profile");
            host.SetBindingProfile(profile);
            EditorUtility.SetDirty(host);

            var dispatcher = host.GetComponent<WebViewActionDispatcher>();
            if (dispatcher == null)
            {
                dispatcher = Undo.AddComponent<WebViewActionDispatcher>(host.gameObject);
            }
            else
            {
                Undo.RecordObject(dispatcher, "Link WebView Binding Profile");
            }

            dispatcher.Profile = profile;
            SyncDispatcherFromProfile(dispatcher, profile);
            EditorUtility.SetDirty(dispatcher);
            if (host != null)
                EditorUtility.SetDirty(host);
        }

        public static void SyncDispatcherFromProfile(WebViewActionDispatcher dispatcher, WebUIViewBindingProfile profile)
        {
            if (dispatcher == null || profile == null)
                return;

            for (var i = 0; i < profile.Bindings.Count; i++)
            {
                var source = profile.Bindings[i];
                if (string.IsNullOrWhiteSpace(source.ActionId))
                    continue;

                var entry = dispatcher.FindOrCreate(source.ActionId);
                if (string.IsNullOrWhiteSpace(entry.DisplayName) && !string.IsNullOrWhiteSpace(source.DisplayName))
                    entry.SetDisplayName(source.DisplayName);
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    [CustomEditor(typeof(WebViewHost))]
    public sealed class WebViewHostEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var host = (WebViewHost)target;
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Play 模式 ESC 菜单：勾选 Visible On Start 关闭，或挂 WebViewPageToggle。\n" +
                "SetPageVisible(false) 停止抓帧但保留 WebView；UnityEvent 绑在 WebViewActionDispatcher 上。",
                MessageType.Info);
            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Action Mapper"))
                    WebViewActionMapperWindow.OpenWithHost(host);

                if (GUILayout.Button("Add Action Dispatcher"))
                {
                    if (host.GetComponent<WebViewActionDispatcher>() == null)
                        Undo.AddComponent<WebViewActionDispatcher>(host.gameObject);
                }
            }
        }
    }
}
#endif

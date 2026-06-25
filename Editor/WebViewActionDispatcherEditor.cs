#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    [CustomEditor(typeof(WebViewActionDispatcher))]
    public sealed class WebViewActionDispatcherEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Play 模式下 WebView 按钮点击会触发下方 Bindings 中的 UnityEvent。\n" +
                "可从 Hierarchy 拖入 GameObject 到 Object 槽位。",
                MessageType.Info);

            DrawDefaultInspector();
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    /// <summary>
    /// One-click project checks: GPU plugin, D3D11, optional gree fallback.
    /// </summary>
    static class UnityWebUIProjectSetup
    {
        const string SetupDoneKey = "UnityWebUI.ProjectSetupPrompted.v1";
        const string GreePackageId = "net.gree.unity-webview";
        const string GreePackageUrl = "https://github.com/gree/unity-webview.git?path=/dist/package";

        [InitializeOnLoadMethod]
        static void PromptOnFirstImport()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    return;

                if (SessionState.GetBool(SetupDoneKey, false))
                    return;

                if (IsProjectReady(out _))
                {
                    SessionState.SetBool(SetupDoneKey, true);
                    return;
                }

                if (EditorUtility.DisplayDialog(
                        "Unity Web UI",
                        "检测到项目尚未完成 Unity Web UI 初始化。\n\n" +
                        "打开 Setup 窗口可一键检查 D3D11、GPU 插件，并导入 Sample。",
                        "打开 Setup",
                        "稍后"))
                {
                    ShowWindow();
                }

                SessionState.SetBool(SetupDoneKey, true);
            };
        }

        [MenuItem("Window/Unity Web UI/Setup Project", false, 0)]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow<UnityWebUIProjectSetupWindow>(utility: false, title: "Unity Web UI Setup");
            window.minSize = new Vector2(420f, 320f);
            window.Show();
        }

        [MenuItem("Window/Unity Web UI/Install Optional Gree Fallback")]
        public static void InstallGreeFallback()
        {
            if (IsPackageInstalled(GreePackageId))
            {
                EditorUtility.DisplayDialog("Unity Web UI", "net.gree.unity-webview 已安装。", "OK");
                return;
            }

            var request = Client.Add(GreePackageUrl);
            EditorUtility.DisplayProgressBar("Unity Web UI", "Installing net.gree.unity-webview...", 0.5f);
            EditorApplication.update += WaitForGreeInstall;

            void WaitForGreeInstall()
            {
                if (!request.IsCompleted)
                    return;

                EditorApplication.update -= WaitForGreeInstall;
                EditorUtility.ClearProgressBar();

                if (request.Status == StatusCode.Success)
                    Debug.Log("[UnityWebUI] Installed optional fallback: net.gree.unity-webview");
                else
                    Debug.LogWarning("[UnityWebUI] Failed to install gree fallback: " + request.Error?.message);
            }
        }

        public static bool IsProjectReady(out string summary)
        {
            var sb = new StringBuilder();
            var ok = true;

            if (!IsWindowsGpuPlatform)
            {
                sb.AppendLine("• 当前平台非 Windows，GPU 主路径不可用。");
            }
            else
            {
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D11)
                {
                    ok = false;
                    sb.AppendLine("• Graphics API 不是 D3D11（Edit → Project Settings → Player → Other Settings）。");
                }

                if (!WindowsGpuWebViewNative.IsDllPresent())
                {
                    ok = false;
                    sb.AppendLine("• 缺少 UnityWebUI.WebView2Gpu.dll。");
                    sb.AppendLine("  包根: " + (UnityWebUIPackagePaths.TryGetPackageRoot() ?? "(未解析)"));
                    sb.AppendLine("  查找: " + WindowsGpuWebViewNative.GetPluginDllPath());
                }
            }

            if (ok)
                summary = "项目已满足 Windows GPU 主路径要求。";
            else
                summary = sb.ToString().TrimEnd();

            return ok;
        }

        public static bool IsWindowsGpuPlatform =>
            Application.platform == RuntimePlatform.WindowsEditor ||
            Application.platform == RuntimePlatform.WindowsPlayer;

        public static bool IsPackageInstalled(string packageId)
        {
            var request = Client.List(true, false);
            while (!request.IsCompleted)
            { }

            if (request.Status != StatusCode.Success)
                return false;

            foreach (var package in request.Result)
            {
                if (package.name == packageId)
                    return true;
            }

            return false;
        }

        public static void ApplyGpuPlugin()
        {
            WebViewGpuPluginDeploy.ClosePreviewWindows();
            if (WebViewGpuPluginDeploy.TryDeploy(out var message))
            {
                AssetDatabase.Refresh();
                Debug.Log("[UnityWebUI] " + message);
            }
            else
            {
                Debug.LogWarning("[UnityWebUI] " + message);
            }
        }

        public static void ImportBasicSample()
        {
            var samplePath = UnityWebUIEditorPackagePaths.PackageRoot + "/Samples~/BasicWebUI";
            if (!System.IO.Directory.Exists(samplePath))
            {
                Debug.LogWarning("[UnityWebUI] Basic sample not found at " + samplePath);
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Unity Web UI",
                    "请通过 Package Manager → Unity Web UI → Samples → Basic Web UI Page → Import 导入示例。\n\n" +
                    "导入后把 WebUI/index.html 复制到 Assets/StreamingAssets/WebUI/。",
                    "打开 Package Manager",
                    "知道了"))
                return;

            UnityEditor.PackageManager.UI.Window.Open("com.unitywebui.core");
        }
    }

    sealed class UnityWebUIProjectSetupWindow : EditorWindow
    {
        Vector2 _scroll;

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Unity Web UI — 一键 Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Windows 主路径：D3D11 + 预置 GPU DLL + WebView2 Runtime。\n" +
                "gree 为可选 CPU 回退，不装也能编译；GPU 不可用时再装即可。",
                MessageType.Info);

            EditorGUILayout.Space(8);
            DrawStatusSection();
            EditorGUILayout.Space(8);
            DrawActionsSection();

            EditorGUILayout.EndScrollView();
        }

        void DrawStatusSection()
        {
            EditorGUILayout.LabelField("当前状态", EditorStyles.boldLabel);

            DrawRow("平台", UnityWebUIProjectSetup.IsWindowsGpuPlatform ? "Windows" : Application.platform.ToString());
            DrawRow("Graphics API", SystemInfo.graphicsDeviceType.ToString(),
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11);
            DrawRow("GPU DLL", WindowsGpuWebViewNative.IsDllPresent() ? "已找到" : "缺失",
                WindowsGpuWebViewNative.IsDllPresent());
            DrawRow("包根目录", UnityWebUIPackagePaths.TryGetPackageRoot() ?? "(未解析)", true);
            DrawRow("DLL 路径", WindowsGpuWebViewNative.GetPluginDllPath(), File.Exists(WindowsGpuWebViewNative.GetPluginDllPath()));
            DrawRow("gree 回退", UnityWebUIProjectSetup.IsPackageInstalled("net.gree.unity-webview") ? "已安装" : "未安装（可选）",
                true);

            var ready = UnityWebUIProjectSetup.IsProjectReady(out var summary);
            EditorGUILayout.HelpBox(summary, ready ? MessageType.Info : MessageType.Warning);
        }

        void DrawActionsSection()
        {
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

            if (GUILayout.Button("Apply / Refresh GPU Plugin", GUILayout.Height(28)))
                UnityWebUIProjectSetup.ApplyGpuPlugin();

            if (GUILayout.Button("Install Optional Gree Fallback", GUILayout.Height(28)))
                UnityWebUIProjectSetup.InstallGreeFallback();

            if (GUILayout.Button("Import Basic Sample (打开 Package Manager)", GUILayout.Height(28)))
                UnityWebUIProjectSetup.ImportBasicSample();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("最小场景步骤", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Canvas → RawImage\n" +
                "2. 空物体 + WebViewHost（Display = RawImage，Html = WebUI/index.html）\n" +
                "3. Window → Unity Web UI → Action Mapper 绑定按钮\n" +
                "4. Play",
                MessageType.None);
        }

        static void DrawRow(string label, string value, bool ok = true)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(120));
            var style = ok ? EditorStyles.label : EditorStyles.boldLabel;
            if (!ok)
                GUI.color = new Color(1f, 0.55f, 0.45f);
            EditorGUILayout.LabelField(value, style);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif

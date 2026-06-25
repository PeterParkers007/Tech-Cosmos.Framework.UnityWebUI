#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    public sealed class WebViewPreviewWindow : EditorWindow
    {
        const string PrefLastFile = "UnityWebUI.WebViewPreview.LastFile";
        const string PrefAutoReload = "UnityWebUI.WebViewPreview.AutoReload";

        WebViewEditorPreviewCore _preview;
        VisualElement _previewSurface;
        Image _previewImage;
        Label _statusLabel;
        Toggle _autoReloadToggle;
        string _filePath;
        string _lastStatusText;
        string _cachedLoadedStatus;

        [MenuItem("Window/Unity Web UI/WebView Preview")]
        public static void Open()
        {
            var window = GetWindow<WebViewPreviewWindow>("WebView Preview");
            window.minSize = new Vector2(480, 360);
            window.Show();
        }

        [MenuItem("Assets/Open in WebView Preview", false, 2500)]
        static void OpenSelectedAsset()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
                return;
            OpenWithFile(Path.GetFullPath(path));
        }

        [MenuItem("Assets/Open in WebView Preview", true)]
        static bool OpenSelectedAssetValidate()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return !string.IsNullOrEmpty(path) &&
                   path.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
        }

        public static void OpenWithFile(string htmlFilePath)
        {
            var window = GetWindow<WebViewPreviewWindow>("WebView Preview");
            window.minSize = new Vector2(480, 360);
            window.Show();
            window.LoadFile(htmlFilePath);
        }

        void OnEnable()
        {
            _filePath = EditorPrefs.GetString(PrefLastFile, string.Empty);
            _preview = new WebViewEditorPreviewCore(enableHover: false);
            _preview.ActionClicked += OnActionClicked;
            _preview.NavigateRequested += OnNavigateRequested;
            _preview.RepaintRequested += OnPreviewRepaintRequested;

            BuildUi();
            _preview.Bind(_previewSurface, _previewImage);

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
                LoadFile(_filePath);
            else
                LoadSample();
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _preview?.Unbind();
            _preview?.Dispose();
            _preview = null;
        }

        void OnFocus() => _preview?.SetPaused(false);

        void OnLostFocus() => _preview?.SetPaused(true);

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode &&
                state != PlayModeStateChange.EnteredEditMode)
                return;

            if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
                LoadFile(_filePath);
            else
                LoadSample();
        }

        static Button CreateToolbarButton(string label, Action onClick)
        {
            var button = new Button(onClick) { text = label };
            button.style.marginRight = 4;
            button.style.height = 22;
            return button;
        }

        void BuildUi()
        {
            rootVisualElement.Clear();

            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexShrink = 0,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingTop = 2,
                    paddingBottom = 2,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.15f, 0.15f, 0.15f),
                    backgroundColor = new Color(0.22f, 0.22f, 0.22f),
                }
            };

            toolbar.Add(CreateToolbarButton("Open HTML", PickFile));
            toolbar.Add(CreateToolbarButton("Reload", ReloadCurrent));
            toolbar.Add(CreateToolbarButton("Sample", LoadSample));
            toolbar.Add(CreateToolbarButton("Action Mapper", OpenActionMapper));
            _autoReloadToggle = new Toggle("Auto Reload") { value = EditorPrefs.GetBool(PrefAutoReload, true) };
            _autoReloadToggle.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(PrefAutoReload, evt.newValue));
            _autoReloadToggle.style.marginLeft = 8;
            toolbar.Add(_autoReloadToggle);

            _previewSurface = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = new Color(0.08f, 0.08f, 0.1f),
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            _previewImage = new Image
            {
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore,
                style =
                {
                    flexGrow = 1,
                    width = Length.Percent(100),
                    height = Length.Percent(100),
                }
            };
            _previewSurface.Add(_previewImage);

            _statusLabel = new Label("WebView Preview")
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    paddingLeft = 8,
                    height = 24,
                    borderTopWidth = 1,
                    borderTopColor = new Color(0.15f, 0.15f, 0.15f),
                    backgroundColor = new Color(0.28f, 0.28f, 0.28f),
                    color = new Color(0.85f, 0.85f, 0.85f),
                }
            };

            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.Add(toolbar);
            rootVisualElement.Add(_previewSurface);
            rootVisualElement.Add(_statusLabel);
        }

        void PickFile()
        {
            var path = EditorUtility.OpenFilePanel("Open HTML", string.IsNullOrEmpty(_filePath) ? "" : Path.GetDirectoryName(_filePath), "html");
            if (string.IsNullOrEmpty(path))
                return;
            LoadFile(path);
        }

        void LoadSample()
        {
            var sample = Path.Combine(Application.streamingAssetsPath, "WebUI", "sample", "index.html");
            if (File.Exists(sample))
                LoadFile(sample);
            else
                SetStatus("Sample not found in StreamingAssets/WebUI/sample/index.html");
        }

        void OpenActionMapper()
        {
            WebViewActionMapperWindow.OpenWithFile(_filePath);
        }

        void LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                SetStatus($"File not found: {path}");
                return;
            }

            _filePath = Path.GetFullPath(path);
            EditorPrefs.SetString(PrefLastFile, _filePath);
            _cachedLoadedStatus = null;

            _preview?.LoadHtml(_filePath);
            SetStatus(BuildStatus("loading"));
        }

        void ReloadCurrent()
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            {
                LoadSample();
                return;
            }

            LoadFile(_filePath);
        }

        void OnEditorUpdate()
        {
            if (_preview == null)
                return;

            _preview.SetPaused(!hasFocus);
            _preview.EditorUpdate(_autoReloadToggle != null && _autoReloadToggle.value);
            _preview.RepaintIfNeeded();

            if (_preview.Session?.Backend != null && !_preview.Session.Backend.IsAvailable && _previewImage.style.display == DisplayStyle.None)
                ShowPreviewHint();
        }

        void OnPreviewRepaintRequested()
        {
            if (_preview.Session?.Backend?.Texture != null)
            {
                _previewSurface?.Q<Label>("preview-hint")?.RemoveFromHierarchy();
                if (_cachedLoadedStatus == null)
                    _cachedLoadedStatus = BuildStatus("loaded");
                UpdateStatusIfChanged(_cachedLoadedStatus);
                return;
            }

            if (_preview.Session?.Backend != null && !_preview.Session.Backend.IsAvailable)
                ShowPreviewHint();
            else
                UpdateStatusIfChanged(BuildStatus("loading"));
        }

        void UpdateStatusIfChanged(string status)
        {
            if (status == _lastStatusText)
                return;
            _lastStatusText = status;
            SetStatus(status);
        }

        void ShowPreviewHint()
        {
            if (_previewSurface == null)
                return;

            if (_previewImage.style.display != DisplayStyle.None)
            {
                _previewImage.style.display = DisplayStyle.None;
                _previewImage.image = null;
            }

            var existing = _previewSurface.Q<Label>("preview-hint");
            if (existing != null)
                return;

            _previewSurface.Add(new Label(GetPreviewHintMessage())
            {
                name = "preview-hint",
                style =
                {
                    color = Color.white,
                    whiteSpace = WhiteSpace.Normal,
                    maxWidth = 520,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    position = Position.Absolute,
                }
            });
        }

        static string GetPreviewHintMessage()
        {
            return "WebView 未初始化\n\n" +
                   "请确认：\n" +
                   "• Windows 已安装 WebView2 Runtime\n" +
                   "• GPU 插件已部署（UnityWebUI.WebView2Gpu.dll）\n" +
                   "• Project Settings → Player → Windows → Direct3D11 优先\n\n" +
                   "Editor 预览无需 Play，打开 HTML 后稍等片刻即可。";
        }

        void OnActionClicked(string actionId)
        {
            SetStatus(BuildStatus($"action:{actionId}"));
            Debug.Log($"UnityWebUI action: {actionId}");
        }

        void OnNavigateRequested(string href)
        {
            SetStatus(BuildStatus($"navigate:{href}"));
            Debug.Log($"UnityWebUI navigate: {href}");
        }

        string BuildStatus(string extra)
        {
            var backend = _preview?.Session?.Backend;
            var size = _previewSurface != null
                ? $"{Mathf.RoundToInt(_previewSurface.contentRect.width)}×{Mathf.RoundToInt(_previewSurface.contentRect.height)}"
                : "?";
            var texSize = backend?.Texture != null
                ? $"{backend.Texture.width}x{backend.Texture.height}"
                : "?";
            var file = string.IsNullOrEmpty(_filePath) ? "(none)" : _filePath;
            var plugin = WebViewEditorPreviewCore.DescribeBackend(backend);
            var views = WebViewPerformanceHub.ActiveCount;
            return $"{file}  |  panel:{size} tex:{texSize}  |  {plugin}  |  views:{views}  |  {extra}";
        }

        void SetStatus(string message)
        {
            if (_statusLabel != null)
                _statusLabel.text = message;
        }
    }
}
#endif

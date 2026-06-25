#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    public sealed class WebViewActionMapperWindow : EditorWindow
    {
        const string PrefLastHtml = "UnityWebUI.ActionMapper.LastHtml";
        const string PrefAutoReload = "UnityWebUI.ActionMapper.AutoReload";

        enum ActionRowKind
        {
            Bound,
            Unbound,
            Orphan
        }

        sealed class ActionRow
        {
            public string ActionId;
            public ActionRowKind Kind;
            public string Hint;
            public bool IsAutoButton;
            public int BindingIndex = -1;
        }

        WebViewEditorPreviewCore _preview;
        WebViewHost _targetHost;
        WebUIViewBindingProfile _profile;
        SerializedObject _bindingSerialized;
        bool _bindingsOnDispatcher;

        VisualElement _previewSurface;
        Image _previewImage;
        ListView _actionList;
        VisualElement _bindingPanel;
        Label _statusLabel;
        ObjectField _hostField;
        ObjectField _profileField;
        Toggle _autoReloadToggle;

        string _filePath;
        string _lastStatusText;
        string _cachedLoadedStatus;

        readonly List<ActionRow> _rows = new List<ActionRow>();
        int _selectedBindingIndex = -1;
        string _selectedActionId;

        [MenuItem("Window/Unity Web UI/Action Mapper")]
        public static void Open()
        {
            var window = GetWindow<WebViewActionMapperWindow>("WebView Action Mapper");
            window.minSize = new Vector2(960, 560);
            window.Show();
        }

        public static void OpenWithHost(WebViewHost host)
        {
            var window = GetWindow<WebViewActionMapperWindow>("WebView Action Mapper");
            window.minSize = new Vector2(960, 560);
            window.Show();
            window.SetTargetHost(host);
        }

        public static void OpenWithFile(string htmlFilePath, WebViewHost host = null)
        {
            var window = GetWindow<WebViewActionMapperWindow>("WebView Action Mapper");
            window.minSize = new Vector2(960, 560);
            window.Show();
            if (host != null)
                window.SetTargetHost(host);
            if (!string.IsNullOrEmpty(htmlFilePath) && File.Exists(htmlFilePath))
                window.LoadFile(htmlFilePath);
        }

        void OnEnable()
        {
            _filePath = EditorPrefs.GetString(PrefLastHtml, string.Empty);
            _preview = new WebViewEditorPreviewCore(enableHover: true);
            _preview.ActionClicked += OnPreviewActionClicked;
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
            _bindingSerialized = null;
        }

        void OnFocus()
        {
            _preview?.SetPaused(false);
        }

        void OnLostFocus()
        {
            _preview?.SetPaused(true);
        }

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
                    alignItems = Align.Center,
                }
            };

            toolbar.Add(CreateToolbarButton("Open HTML", PickFile));
            toolbar.Add(CreateToolbarButton("Reload", ReloadCurrent));
            toolbar.Add(CreateToolbarButton("Sample", LoadSample));

            _autoReloadToggle = new Toggle("Auto Reload")
            {
                value = EditorPrefs.GetBool(PrefAutoReload, false),
                style = { marginLeft = 8 }
            };
            _autoReloadToggle.RegisterValueChangedCallback(evt =>
                EditorPrefs.SetBool(PrefAutoReload, evt.newValue));
            toolbar.Add(_autoReloadToggle);

            _hostField = new ObjectField("Host")
            {
                objectType = typeof(WebViewHost),
                allowSceneObjects = true,
                style = { marginLeft = 12, minWidth = 220 }
            };
            _hostField.RegisterValueChangedCallback(evt => SetTargetHost(evt.newValue as WebViewHost));
            toolbar.Add(_hostField);

            toolbar.Add(CreateToolbarButton("Pick Host", PickHostFromSelection));

            _profileField = new ObjectField("Profile")
            {
                objectType = typeof(WebUIViewBindingProfile),
                allowSceneObjects = false,
                style = { marginLeft = 8, minWidth = 220 }
            };
            _profileField.RegisterValueChangedCallback(evt =>
                AssignProfile(evt.newValue as WebUIViewBindingProfile, linkHost: false, rebuildRows: true));
            toolbar.Add(_profileField);

            var body = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexGrow = 1 }
            };

            _previewSurface = new VisualElement
            {
                style =
                {
                    flexBasis = new Length(42, LengthUnit.Percent),
                    flexGrow = 1,
                    backgroundColor = new Color(0.08f, 0.08f, 0.1f),
                    borderRightWidth = 1,
                    borderRightColor = new Color(0.15f, 0.15f, 0.15f),
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

            var listColumn = new VisualElement
            {
                style =
                {
                    flexBasis = new Length(28, LengthUnit.Percent),
                    flexGrow = 0,
                    flexShrink = 0,
                    borderRightWidth = 1,
                    borderRightColor = new Color(0.15f, 0.15f, 0.15f),
                    backgroundColor = new Color(0.18f, 0.18f, 0.2f),
                }
            };

            listColumn.Add(new Label("Buttons / Actions")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 8,
                    paddingTop = 6,
                    paddingBottom = 4,
                }
            });

            _actionList = new ListView
            {
                itemsSource = _rows,
                selectionType = SelectionType.Single,
                fixedItemHeight = 44,
                style = { flexGrow = 1 }
            };
            _actionList.makeItem = MakeActionRow;
            _actionList.bindItem = BindActionRow;
            _actionList.selectionChanged += _ => OnActionSelectionChanged();
            listColumn.Add(_actionList);

            var buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingBottom = 6,
                }
            };
            buttonRow.Add(CreateToolbarButton("Rescan", RescanActions));
            buttonRow.Add(CreateToolbarButton("Add", AddManualAction));
            buttonRow.Add(CreateToolbarButton("Remove Orphans", RemoveOrphanBindings));
            listColumn.Add(buttonRow);

            var inspectorColumn = new VisualElement
            {
                style =
                {
                    flexBasis = new Length(30, LengthUnit.Percent),
                    flexGrow = 1,
                    backgroundColor = new Color(0.2f, 0.2f, 0.22f),
                }
            };

            inspectorColumn.Add(new Label("Binding")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 8,
                    paddingTop = 6,
                    paddingBottom = 4,
                }
            });

            _bindingPanel = new VisualElement
            {
                style = { flexGrow = 1, paddingLeft = 6, paddingRight = 6 }
            };
            inspectorColumn.Add(_bindingPanel);

            body.Add(_previewSurface);
            body.Add(listColumn);
            body.Add(inspectorColumn);

            _statusLabel = new Label("Action Mapper | 自动扫描 <button> | Edit Mode 点击仅 Log")
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
            rootVisualElement.Add(body);
            rootVisualElement.Add(_statusLabel);
            RebuildBindingPanel();
        }

        static VisualElement MakeActionRow()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.Center,
                    paddingLeft = 8,
                    paddingRight = 8,
                }
            };
            row.Add(new Label { name = "title" });
            row.Add(new Label { name = "subtitle" });
            return row;
        }

        void BindActionRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _rows.Count)
                return;

            var row = _rows[index];
            var title = element.Q<Label>("title");
            var subtitle = element.Q<Label>("subtitle");

            var prefix = row.Kind switch
            {
                ActionRowKind.Bound => "[OK] ",
                ActionRowKind.Unbound => "[--] ",
                ActionRowKind.Orphan => "[!!] ",
                _ => string.Empty
            };

            title.text = prefix + row.ActionId;
            title.style.color = row.Kind switch
            {
                ActionRowKind.Bound => new Color(0.65f, 0.95f, 0.65f),
                ActionRowKind.Unbound => new Color(0.95f, 0.85f, 0.55f),
                ActionRowKind.Orphan => new Color(0.95f, 0.6f, 0.6f),
                _ => Color.white
            };

            subtitle.text = string.IsNullOrEmpty(row.Hint)
                ? (row.IsAutoButton ? "HTML <button>  |  " : string.Empty) + KindHint(row.Kind)
                : row.Hint + (row.IsAutoButton ? "  |  <button>" : string.Empty) + "  |  " + KindHint(row.Kind);
            subtitle.style.fontSize = 10;
            subtitle.style.color = new Color(0.75f, 0.75f, 0.75f);
        }

        static string KindHint(ActionRowKind kind)
        {
            return kind switch
            {
                ActionRowKind.Bound => "bound",
                ActionRowKind.Unbound => "unbound",
                ActionRowKind.Orphan => "orphan (not in HTML)",
                _ => string.Empty
            };
        }

        static Button CreateToolbarButton(string label, Action onClick)
        {
            var button = new Button(onClick) { text = label };
            button.style.marginRight = 4;
            button.style.height = 22;
            return button;
        }

        static Label CreateHelpLabel(string text, Color color)
        {
            return new Label(text)
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 6,
                    color = color,
                    fontSize = 11,
                }
            };
        }

        void RebuildBindingPanel()
        {
            if (_bindingPanel == null)
                return;

            _bindingPanel.Clear();

            if (_profile == null)
            {
                _bindingPanel.Add(CreateHelpLabel(
                    "Load an HTML file to create or locate a binding profile.",
                    new Color(0.8f, 0.85f, 0.95f)));
                return;
            }

            if (_targetHost == null)
            {
                _bindingPanel.Add(CreateHelpLabel(
                    "请先将场景中带有 WebViewHost 的 GameObject 拖到工具栏 Host 槽位。\n" +
                    "UnityEvent 必须绑在场景组件上，才能从 Hierarchy 拖入 GameObject。",
                    new Color(1f, 0.85f, 0.55f)));
            }
            else
            {
                var dispatcher = GetTargetDispatcher(create: true);
                if (dispatcher != null)
                {
                    _bindingPanel.Add(CreateHelpLabel(
                        $"绑定目标：{dispatcher.name} / WebViewActionDispatcher",
                        new Color(0.75f, 0.9f, 0.75f)));
                    _bindingPanel.Add(new Button(() =>
                    {
                        Selection.activeObject = dispatcher;
                        EditorGUIUtility.PingObject(dispatcher);
                    })
                    {
                        text = "在 Inspector 中编辑（推荐，拖 Hierarchy 最稳定）",
                        style = { height = 22, marginBottom = 6, alignSelf = Align.FlexStart }
                    });
                }
            }

            if (_selectedBindingIndex < 0)
            {
                _bindingPanel.Add(CreateHelpLabel(
                    "在左侧选择一个按钮，然后在下方 On Invoked 绑定 UnityEvent。",
                    new Color(0.8f, 0.85f, 0.95f)));
                return;
            }

            RefreshBindingSerializedObject();
            if (_bindingSerialized == null)
                return;

            _bindingSerialized.Update();
            var bindings = _bindingSerialized.FindProperty("_bindings");
            if (_selectedBindingIndex >= bindings.arraySize)
                return;

            var entry = bindings.GetArrayElementAtIndex(_selectedBindingIndex);
            _bindingPanel.Add(new Label($"Action ID: {entry.FindPropertyRelative("_actionId").stringValue}")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 }
            });

            var displayNameProp = entry.FindPropertyRelative("_displayName");
            var displayNameField = new PropertyField(displayNameProp, "Display Name");
            displayNameField.BindProperty(displayNameProp);
            displayNameField.RegisterValueChangeCallback(_ => OnBindingPropertyChanged());
            _bindingPanel.Add(displayNameField);

            var onInvokedProp = entry.FindPropertyRelative("_onInvoked");
            var onInvokedField = new PropertyField(onInvokedProp, "On Invoked");
            onInvokedField.BindProperty(onInvokedProp);
            onInvokedField.RegisterValueChangeCallback(_ => OnBindingPropertyChanged());
            onInvokedField.style.marginTop = 4;
            onInvokedField.style.flexGrow = 1;
            _bindingPanel.Add(onInvokedField);
        }

        void OnBindingPropertyChanged()
        {
            if (_bindingSerialized == null)
                return;

            Undo.RecordObject(_bindingSerialized.targetObject, "Edit WebView Binding");
            _bindingSerialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_bindingSerialized.targetObject);
            UpdateActionRowStates();
        }

        WebViewActionDispatcher GetTargetDispatcher(bool create)
        {
            if (_targetHost == null)
                return null;

            var dispatcher = _targetHost.GetComponent<WebViewActionDispatcher>();
            if (dispatcher != null || !create)
                return dispatcher;

            dispatcher = Undo.AddComponent<WebViewActionDispatcher>(_targetHost.gameObject);
            dispatcher.Profile = _profile;
            if (_profile != null)
                WebViewBindingProfileUtility.SyncDispatcherFromProfile(dispatcher, _profile);
            return dispatcher;
        }

        void RefreshBindingSerializedObject()
        {
            var dispatcher = GetTargetDispatcher(create: false);
            _bindingsOnDispatcher = dispatcher != null;
            if (dispatcher != null)
                _bindingSerialized = new SerializedObject(dispatcher);
            else
                _bindingSerialized = _profile != null ? new SerializedObject(_profile) : null;
        }

        int IndexOfBinding(string actionId)
        {
            var dispatcher = GetTargetDispatcher(create: false);
            if (dispatcher != null)
                return dispatcher.IndexOf(actionId);

            return _profile != null ? _profile.IndexOf(actionId) : -1;
        }

        WebViewActionBindingEntry GetBindingEntry(int index)
        {
            var dispatcher = GetTargetDispatcher(create: false);
            if (dispatcher != null && index >= 0 && index < dispatcher.Bindings.Count)
                return dispatcher.Bindings[index];

            if (_profile != null && index >= 0 && index < _profile.Bindings.Count)
                return _profile.Bindings[index];

            return null;
        }

        void SyncScannedBindings(IReadOnlyList<HtmlActionScanner.ScannedAction> scanned)
        {
            if (scanned == null || _profile == null)
                return;

            _profile.SyncWithScannedActions(scanned);
            EditorUtility.SetDirty(_profile);

            var dispatcher = GetTargetDispatcher(create: _targetHost != null);
            if (dispatcher == null)
                return;

            dispatcher.SyncWithScannedActions(scanned);
            if (_profile != null && dispatcher.Profile != _profile)
                dispatcher.Profile = _profile;
            EditorUtility.SetDirty(dispatcher);
        }

        void SetTargetHost(WebViewHost host)
        {
            _targetHost = host;
            _hostField?.SetValueWithoutNotify(host);

            if (host == null)
            {
                RefreshBindingSerializedObject();
                RebuildBindingPanel();
                return;
            }

            var dispatcher = GetTargetDispatcher(create: true);
            if (_profile != null)
            {
                WebViewBindingProfileUtility.LinkHostToProfile(host, _profile);
                if (dispatcher != null)
                    WebViewBindingProfileUtility.SyncDispatcherFromProfile(dispatcher, _profile);
                RefreshBindingSerializedObject();
                RebuildActionRows();
                return;
            }

            if (host.BindingProfile != null)
                AssignProfile(host.BindingProfile, linkHost: false, rebuildRows: true);
            else if (dispatcher?.Profile != null)
                AssignProfile(dispatcher.Profile, linkHost: false, rebuildRows: true);
            else
            {
                RefreshBindingSerializedObject();
                RebuildBindingPanel();
            }
        }

        void PickHostFromSelection()
        {
            var host = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<WebViewHost>()
                : null;
            if (host == null)
            {
                SetStatus("Select a GameObject with WebViewHost in the scene.");
                return;
            }

            SetTargetHost(host);
        }

        void AssignProfile(WebUIViewBindingProfile profile, bool linkHost, bool rebuildRows)
        {
            if (_profile == profile && !rebuildRows)
                return;

            _profile = profile;
            _profileField?.SetValueWithoutNotify(profile);
            RefreshBindingSerializedObject();

            if (rebuildRows)
                RebuildActionRows();

            if (linkHost && _targetHost != null && profile != null)
                WebViewBindingProfileUtility.LinkHostToProfile(_targetHost, profile);

            RefreshBindingSerializedObject();
            RebuildBindingPanel();
        }

        void PickFile()
        {
            var path = EditorUtility.OpenFilePanel(
                "Open HTML",
                string.IsNullOrEmpty(_filePath) ? "" : Path.GetDirectoryName(_filePath),
                "html");
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

        void LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                SetStatus($"File not found: {path}");
                return;
            }

            _filePath = Path.GetFullPath(path);
            EditorPrefs.SetString(PrefLastHtml, _filePath);

            var profile = WebViewBindingProfileUtility.GetOrCreateProfileForHtml(_filePath);
            if (!profile.MatchesHtmlPath(_filePath))
            {
                profile.SetSourceHtmlPath(_filePath);
                EditorUtility.SetDirty(profile);
            }

            AssignProfile(profile, linkHost: _targetHost != null, rebuildRows: true);
            _preview?.LoadHtml(_filePath);
            _cachedLoadedStatus = null;
            SetStatus(BuildStatus("loading"));
        }

        void RescanActions() => RebuildActionRows();

        void RebuildActionRows()
        {
            var preserveId = _selectedActionId;
            _rows.Clear();

            if (_profile == null)
            {
                _selectedBindingIndex = -1;
                _selectedActionId = null;
                _actionList?.Rebuild();
                return;
            }

            if (!string.IsNullOrEmpty(_filePath))
            {
                var scanned = HtmlActionScanner.ScanFile(_filePath);
                SyncScannedBindings(scanned);
                PopulateRows(scanned);
            }
            else
            {
                PopulateRows(Array.Empty<HtmlActionScanner.ScannedAction>());
            }

            _actionList?.Rebuild();
            RestoreSelection(preserveId);
            RebuildBindingPanel();
        }

        void PopulateRows(IReadOnlyList<HtmlActionScanner.ScannedAction> scanned)
        {
            var scannedIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < scanned.Count; i++)
            {
                var action = scanned[i];
                scannedIds.Add(action.Id);
                var bindingIndex = IndexOfBinding(action.Id);
                var entry = GetBindingEntry(bindingIndex);
                _rows.Add(new ActionRow
                {
                    ActionId = action.Id,
                    BindingIndex = bindingIndex,
                    Hint = BuildHint(action.TagHint, action.TextHint),
                    IsAutoButton = action.IsButton,
                    Kind = entry != null && entry.HasListeners ? ActionRowKind.Bound : ActionRowKind.Unbound
                });
            }

            var orphanSource = GetTargetDispatcher(create: false)?.Bindings ?? _profile.Bindings;
            for (var i = 0; i < orphanSource.Count; i++)
            {
                var entry = orphanSource[i];
                if (scannedIds.Contains(entry.ActionId))
                    continue;

                var bindingIndex = IndexOfBinding(entry.ActionId);
                _rows.Add(new ActionRow
                {
                    ActionId = entry.ActionId,
                    BindingIndex = bindingIndex,
                    Hint = entry.DisplayName,
                    Kind = ActionRowKind.Orphan
                });
            }

            _rows.Sort((a, b) => string.Compare(a.ActionId, b.ActionId, StringComparison.Ordinal));
        }

        void UpdateActionRowStates()
        {
            if (_profile == null)
                return;

            var bindingCount = GetTargetDispatcher(create: false)?.Bindings.Count ?? _profile.Bindings.Count;
            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.BindingIndex < 0 || row.BindingIndex >= bindingCount)
                {
                    row.Kind = ActionRowKind.Unbound;
                    continue;
                }

                var entry = GetBindingEntry(row.BindingIndex);
                row.Kind = row.Kind == ActionRowKind.Orphan
                    ? ActionRowKind.Orphan
                    : entry != null && entry.HasListeners ? ActionRowKind.Bound : ActionRowKind.Unbound;
            }

            RefreshActionListSafely();
        }

        void RefreshActionListSafely()
        {
            if (_actionList == null)
                return;

            try
            {
                _actionList.RefreshItems();
            }
            catch
            {
                _actionList.Rebuild();
            }
        }

        static string BuildHint(string tagHint, string textHint)
        {
            if (!string.IsNullOrEmpty(tagHint) && !string.IsNullOrEmpty(textHint))
                return $"<{tagHint}> {textHint}";
            if (!string.IsNullOrEmpty(textHint))
                return textHint;
            if (!string.IsNullOrEmpty(tagHint))
                return $"<{tagHint}>";
            return string.Empty;
        }

        void OnActionSelectionChanged()
        {
            if (_actionList == null || _actionList.selectedIndex < 0 || _actionList.selectedIndex >= _rows.Count)
            {
                _selectedBindingIndex = -1;
                _selectedActionId = null;
            }
            else
            {
                var row = _rows[_actionList.selectedIndex];
                _selectedBindingIndex = row.BindingIndex;
                _selectedActionId = row.ActionId;
            }

            RefreshBindingSerializedObject();
            RebuildBindingPanel();
        }

        void RestoreSelection(string actionId)
        {
            if (string.IsNullOrEmpty(actionId) || _actionList == null)
            {
                OnActionSelectionChanged();
                return;
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                if (!string.Equals(_rows[i].ActionId, actionId, StringComparison.Ordinal))
                    continue;

                _actionList.SetSelection(i);
                return;
            }

            OnActionSelectionChanged();
        }

        void AddManualAction()
        {
            if (_profile == null)
                return;

            var id = EditorInputDialog.Show("Add Action", "Action ID", "my-action");
            if (string.IsNullOrWhiteSpace(id))
                return;

            id = id.Trim();
            _profile.FindOrCreate(id);
            EditorUtility.SetDirty(_profile);

            var dispatcher = GetTargetDispatcher(create: _targetHost != null);
            dispatcher?.FindOrCreate(id);
            if (dispatcher != null)
                EditorUtility.SetDirty(dispatcher);

            RebuildActionRows();
        }

        void RemoveOrphanBindings()
        {
            if (_profile == null || string.IsNullOrEmpty(_filePath))
                return;

            var scanned = HtmlActionScanner.ScanFile(_filePath);
            var valid = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < scanned.Count; i++)
                valid.Add(scanned[i].Id);

            _profile.RemoveOrphans(valid);
            EditorUtility.SetDirty(_profile);

            var dispatcher = GetTargetDispatcher(create: false);
            dispatcher?.RemoveOrphans(valid);
            if (dispatcher != null)
                EditorUtility.SetDirty(dispatcher);

            RebuildActionRows();
        }

        void OnPreviewActionClicked(string actionId)
        {
            SelectActionRow(actionId);
            SetStatus(BuildStatus($"preview action:{actionId} (Edit Mode: log only)"));
            Debug.Log($"UnityWebUI Action Mapper preview: {actionId}");
        }

        void OnNavigateRequested(string href)
        {
            SetStatus(BuildStatus($"navigate:{href}"));
            Debug.Log($"UnityWebUI Action Mapper navigate: {href}");
        }

        void SelectActionRow(string actionId)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                if (!string.Equals(_rows[i].ActionId, actionId, StringComparison.Ordinal))
                    continue;

                _actionList?.SetSelection(i);
                break;
            }
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
        }

        void OnPreviewRepaintRequested()
        {
            if (_cachedLoadedStatus == null && _preview?.Session?.Backend?.Texture != null)
                _cachedLoadedStatus = BuildStatus("loaded");
            UpdateStatusIfChanged(_cachedLoadedStatus ?? BuildStatus(_preview.IsReady ? "loading" : "backend missing"));
        }

        void UpdateStatusIfChanged(string status)
        {
            if (status == _lastStatusText)
                return;
            _lastStatusText = status;
            SetStatus(status);
        }

        string BuildStatus(string extra)
        {
            var bound = 0;
            var unbound = 0;
            var orphan = 0;
            for (var i = 0; i < _rows.Count; i++)
            {
                switch (_rows[i].Kind)
                {
                    case ActionRowKind.Bound: bound++; break;
                    case ActionRowKind.Unbound: unbound++; break;
                    case ActionRowKind.Orphan: orphan++; break;
                }
            }

            var file = string.IsNullOrEmpty(_filePath) ? "(none)" : Path.GetFileName(_filePath);
            var host = _targetHost != null ? _targetHost.name : "(none)";
            var profile = _profile != null ? _profile.name : "(none)";
            var backend = WebViewEditorPreviewCore.DescribeBackend(_preview?.Session?.Backend);
            var capture = _preview?.Session?.Backend?.Texture != null
                ? $"{_preview.Session.Backend.Texture.width}x{_preview.Session.Backend.Texture.height}"
                : "?";
            return $"{file} | {capture} | {backend} | host:{host} | profile:{profile} | bound:{bound} unbound:{unbound} orphan:{orphan} | {extra}";
        }

        void SetStatus(string message)
        {
            if (_statusLabel != null)
                _statusLabel.text = message;
        }
    }

    static class EditorInputDialog
    {
        public static string Show(string title, string message, string defaultValue)
        {
            var window = ScriptableObject.CreateInstance<InputDialogWindow>();
            window.titleContent = new GUIContent(title);
            window.Initialize(message, defaultValue);
            window.ShowModalUtility();
            return window.Result;
        }

        sealed class InputDialogWindow : EditorWindow
        {
            string _message;
            string _value;

            public string Result { get; private set; }

            public void Initialize(string message, string defaultValue)
            {
                _message = message;
                _value = defaultValue;
                minSize = new Vector2(360, 110);
                maxSize = minSize;
            }

            void OnGUI()
            {
                EditorGUILayout.LabelField(_message);
                GUI.SetNextControlName("InputField");
                _value = EditorGUILayout.TextField(_value);

                if (Event.current.type == EventType.Layout)
                    EditorGUI.FocusTextInControl("InputField");

                EditorGUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Cancel"))
                    {
                        Result = null;
                        Close();
                    }

                    if (GUILayout.Button("OK"))
                    {
                        Result = _value;
                        Close();
                    }
                }

                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                {
                    Result = _value;
                    Close();
                }
            }
        }
    }
}
#endif

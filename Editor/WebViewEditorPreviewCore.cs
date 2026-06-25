#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    /// <summary>
    /// Shared Editor WebView preview: debounced resize, throttled hover input, full capture resolution.
    /// </summary>
    sealed class WebViewEditorPreviewCore : IDisposable
    {
        const double ResizeDebounceSec = 0.2;
        const double FilePollIntervalSec = 0.75;
        const double PointerMoveIntervalSec = 1.0 / 12.0;
        const long MinPressVisualMs = 60;

        readonly WebViewBackendOptions _backendOptions;
        readonly bool _enableHover;

        WebViewSession _session;
        WindowsGpuWebViewBackend _gpuBackend;
        VisualElement _surface;
        Image _previewImage;

        string _filePath;
        DateTime _lastWriteTimeUtc;
        double _lastFilePollTime;
        double _lastResizeScheduleTime;
        Vector2Int _pendingSize;
        Vector2Int _appliedSize;
        int _lastRepaintContentVersion = -1;
        Texture _lastPreviewTexture;
        bool _lastPreviewFlip;
        Vector2 _lastPointer = new Vector2(float.NaN, float.NaN);
        double _lastPointerMoveTime;
        bool _pressActive;
        Vector2 _pressPoint;
        double _pressTime;
        IVisualElementScheduledItem _pendingRelease;

        public WebViewSession Session => _session;
        public string FilePath => _filePath;
        public bool IsReady => _session?.Backend?.IsAvailable == true;

        public event Action<string> ActionClicked;
        public event Action<string> NavigateRequested;
        public event Action RepaintRequested;

        public WebViewEditorPreviewCore(bool enableHover, int refreshCycle = 3, float renderScale = 1f)
        {
            _enableHover = enableHover;
            _backendOptions = new WebViewBackendOptions
            {
                BitmapRefreshCycle = Mathf.Max(1, refreshCycle),
                Priority = WebViewPumpPriority.EditorPreview,
                RenderScale = Mathf.Clamp(renderScale, 0.25f, 1f),
            };
        }

        public static string DescribeBackend(IWebViewBackend backend)
        {
            if (backend == null)
                return "backend:none";

            if (backend is WindowsGpuWebViewBackend gpu)
            {
                var status = WindowsGpuWebViewNative.GetRuntimeStatus(gpu.GpuHandle);
                var path = status.Contains("gpuTex=1") ? "gpuTex" : status.Contains("cpuFrame=1") ? "cpuFrame" : "waiting";
                var apiHint = string.Empty;
                try
                {
                    if (WindowsGpuWebViewNative.WebViewGpu_GetApiVersion() < 13 && path == "cpuFrame")
                        apiHint = " | need api=13 plugin for Editor gpuTex";
                }
                catch
                {
                    // ignored
                }

                return $"gpu/{path}{apiHint} | {status}";
            }

            if (backend is GreeUnityWebViewBackend)
            {
                var reason = WebViewBackendFactory.LastGpuFallbackReason;
                return string.IsNullOrEmpty(reason)
                    ? "gree/cpu-bitmap"
                    : $"gree/cpu-bitmap | {reason}";
            }

            if (backend is VuplexWebViewBackend)
                return "vuplex";

            return backend.IsAvailable ? backend.GetType().Name : "missing";
        }

        public void Bind(VisualElement surface, Image previewImage)
        {
            _surface = surface;
            _previewImage = previewImage;

            if (_surface == null)
                return;

            _surface.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _surface.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _surface.RegisterCallback<PointerUpEvent>(OnPointerUp);
            _surface.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            if (_enableHover)
                _surface.RegisterCallback<PointerMoveEvent>(OnPointerMove);

            _surface.RegisterCallback<WheelEvent>(OnWheel);
            ScheduleResize(force: true);
        }

        public void Unbind()
        {
            CancelPendingRelease();
            _pressActive = false;

            if (_surface != null)
            {
                _surface.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                _surface.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                _surface.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                _surface.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
                if (_enableHover)
                    _surface.UnregisterCallback<PointerMoveEvent>(OnPointerMove);

                _surface.UnregisterCallback<WheelEvent>(OnWheel);
            }

            _surface = null;
            _previewImage = null;
        }

        public void SetPaused(bool paused)
        {
            if (_session?.Backend is IWebViewCaptureSuspend suspend)
            {
                suspend.SetCaptureSuspended(paused);
                return;
            }

            if (_session?.Backend is IWebViewPumpTarget target)
            {
                if (paused)
                    WebViewPerformanceHub.Unregister(target);
                else
                    WebViewPerformanceHub.Register(target);
            }
        }

        public void EditorUpdate(bool autoReloadHtml)
        {
            if (_session == null)
                return;

            var now = EditorApplication.timeSinceStartup;
            if (now - _lastResizeScheduleTime >= ResizeDebounceSec &&
                (_pendingSize.x != _appliedSize.x || _pendingSize.y != _appliedSize.y))
            {
                ApplyPendingResize();
            }

            if (!autoReloadHtml || string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
                return;

            if (now - _lastFilePollTime < FilePollIntervalSec)
                return;

            _lastFilePollTime = now;
            var writeTime = File.GetLastWriteTimeUtc(_filePath);
            if (writeTime > _lastWriteTimeUtc)
            {
                _lastWriteTimeUtc = writeTime;
                ReloadCurrent();
            }
        }

        public void LoadHtml(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            _filePath = Path.GetFullPath(path);
            _lastWriteTimeUtc = File.GetLastWriteTimeUtc(_filePath);
            RecreateSession();
            _session.LoadLocalHtml(_filePath);
            ScheduleResize(force: true);
            RequestRepaint(force: true);
        }

        public void ReloadCurrent()
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
                return;

            LoadHtml(_filePath);
        }

        public void RepaintIfNeeded()
        {
            if (_previewImage == null || _session?.Backend == null)
                return;

            var backend = _session.Backend;
            var contentVersion = backend is WindowsGpuWebViewBackend gpu ? gpu.ContentVersion : -1;
            if (contentVersion >= 0 && contentVersion == _lastRepaintContentVersion && _previewImage.image != null)
                return;

            var texture = backend.Texture;
            if (texture != null)
            {
                if (_previewImage.parent != _surface && _surface != null)
                    _surface.Add(_previewImage);

                if (!ReferenceEquals(_previewImage.image, texture))
                    _previewImage.image = texture;

                _previewImage.style.display = DisplayStyle.Flex;

                var flip = backend.FlipVertically;
                if (_lastPreviewTexture != texture || _lastPreviewFlip != flip)
                {
                    _lastPreviewTexture = texture;
                    _lastPreviewFlip = flip;
                    _previewImage.style.scale = flip
                        ? new Scale(new Vector3(1f, -1f, 1f))
                        : new Scale(Vector3.one);
                }

                _lastRepaintContentVersion = contentVersion;
                RepaintRequested?.Invoke();
                return;
            }

            _lastPreviewTexture = null;
            _lastRepaintContentVersion = -1;
            if (_previewImage != null)
            {
                _previewImage.image = null;
                _previewImage.style.display = DisplayStyle.None;
            }

            RepaintRequested?.Invoke();
        }

        public void Dispose()
        {
            Unbind();
            DisposeSession();
        }

        void RecreateSession()
        {
            DisposeSession();
            _lastRepaintContentVersion = -1;
            _lastPreviewTexture = null;
            _appliedSize = Vector2Int.zero;
            _pendingSize = Vector2Int.zero;
            _session = new WebViewSession(_backendOptions);
            _session.Bridge.ActionClicked += HandleActionClicked;
            _session.Bridge.NavigateRequested += HandleNavigateRequested;
            BindGpuFrameUpdates();
        }

        void DisposeSession()
        {
            if (_gpuBackend != null)
            {
                _gpuBackend.FrameUpdated -= OnGpuFrameUpdated;
                _gpuBackend = null;
            }

            if (_session == null)
                return;

            _session.Bridge.ActionClicked -= HandleActionClicked;
            _session.Bridge.NavigateRequested -= HandleNavigateRequested;
            _session.Dispose();
            _session = null;
        }

        void BindGpuFrameUpdates()
        {
            if (_gpuBackend != null)
                _gpuBackend.FrameUpdated -= OnGpuFrameUpdated;

            _gpuBackend = _session?.Backend as WindowsGpuWebViewBackend;
            if (_gpuBackend != null)
                _gpuBackend.FrameUpdated += OnGpuFrameUpdated;
        }

        void OnGpuFrameUpdated() => RequestRepaint(force: false);

        void RequestRepaint(bool force)
        {
            if (force)
                _lastRepaintContentVersion = -1;
            RepaintIfNeeded();
        }

        void HandleActionClicked(string actionId) => ActionClicked?.Invoke(actionId);

        void HandleNavigateRequested(string href) => NavigateRequested?.Invoke(href);

        void OnGeometryChanged(GeometryChangedEvent _)
        {
            ScheduleResize(force: false);
        }

        void ScheduleResize(bool force)
        {
            if (_surface == null)
                return;

            var rect = _surface.contentRect;
            var width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            var height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            if (width == _pendingSize.x && height == _pendingSize.y && !force)
                return;

            _pendingSize = new Vector2Int(width, height);
            _lastResizeScheduleTime = EditorApplication.timeSinceStartup;

            if (force)
                ApplyPendingResize();
        }

        void ApplyPendingResize()
        {
            if (_session == null || _pendingSize.x <= 0 || _pendingSize.y <= 0)
                return;

            if (_pendingSize == _appliedSize)
                return;

            _appliedSize = _pendingSize;
            _session.SetSize(_appliedSize.x, _appliedSize.y);
            _lastRepaintContentVersion = -1;
            RequestRepaint(force: true);
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (_session?.Backend == null || !_session.Backend.IsAvailable)
                return;

            if (!TryMapPointer(evt.localPosition, out var point))
                return;

            CancelPendingRelease();
            _pressActive = true;
            _pressPoint = point;
            _pressTime = EditorApplication.timeSinceStartup;
            _session.Backend.PointerDown(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
            _lastRepaintContentVersion = -1;
            RequestRepaint(force: true);
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (!_pressActive || _session?.Backend == null || !_session.Backend.IsAvailable)
                return;

            if (!TryMapPointer(evt.localPosition, out var point))
                point = _pressPoint;

            SchedulePointerRelease(point);
            evt.StopPropagation();
        }

        void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (_pressActive)
                SchedulePointerRelease(_pressPoint);

            if (_enableHover)
                ResetPointerLeave();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_enableHover || _session?.Backend == null || !_session.Backend.IsAvailable)
                return;

            var now = EditorApplication.timeSinceStartup;
            if (now - _lastPointerMoveTime < PointerMoveIntervalSec)
                return;

            if (!TryMapPointer(evt.localPosition, out var point))
            {
                ResetPointerLeave();
                return;
            }

            if (point == _lastPointer)
                return;

            _lastPointerMoveTime = now;
            _lastPointer = point;
            _session.Backend.MovePointer(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
        }

        void ResetPointerLeave()
        {
            if (_session?.Backend == null || float.IsNaN(_lastPointer.x))
                return;

            _lastPointer = new Vector2(float.NaN, float.NaN);
            _session.Backend.LeavePointer();
        }

        void CancelPendingRelease()
        {
            _pendingRelease?.Pause();
            _pendingRelease = null;
        }

        void SchedulePointerRelease(Vector2 point)
        {
            CancelPendingRelease();

            var holdMs = (EditorApplication.timeSinceStartup - _pressTime) * 1000.0;
            var delayMs = (long)Mathf.Max(0f, MinPressVisualMs - (float)holdMs);
            if (_surface == null)
            {
                FinishPointerRelease(point);
                return;
            }

            _pendingRelease = _surface.schedule.Execute(() => FinishPointerRelease(point));
            if (delayMs > 0)
                _pendingRelease.ExecuteLater(delayMs);
        }

        void FinishPointerRelease(Vector2 point)
        {
            CancelPendingRelease();
            if (!_pressActive || _session?.Backend == null)
                return;

            _pressActive = false;
            _session.Backend.PointerUp(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y));
            WebViewDomClickUtility.SynthesizeClick(_session.Backend, point);
            _lastRepaintContentVersion = -1;
            RequestRepaint(force: true);
        }

        void OnWheel(WheelEvent evt)
        {
            if (_session?.Backend == null || !_session.Backend.IsAvailable)
                return;

            _session.Backend.Scroll(0, Mathf.RoundToInt(evt.delta.y));
            evt.StopPropagation();
        }

        bool TryMapPointer(Vector2 localPosition, out Vector2 point)
        {
            point = default;
            if (_surface == null || _session?.Backend == null)
                return false;

            var rect = _surface.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return false;

            var texture = _session.Backend.Texture;
            if (texture == null)
                return false;

            var texW = texture.width;
            var texH = texture.height;
            if (texW <= 0 || texH <= 0)
                return false;

            var scale = Mathf.Min(rect.width / texW, rect.height / texH);
            var drawW = texW * scale;
            var drawH = texH * scale;
            var offsetX = (rect.width - drawW) * 0.5f;
            var offsetY = (rect.height - drawH) * 0.5f;

            var x = localPosition.x - offsetX;
            var y = localPosition.y - offsetY;
            if (x < 0f || y < 0f || x > drawW || y > drawH)
                return false;

            var mappedY = _session.Backend.FlipVertically ? y : (drawH - y);
            point = new Vector2(x / scale, mappedY / scale);
            return true;
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityWebUI.WebView;

namespace UnityWebUI.Editor
{
    [InitializeOnLoad]
    static class WebViewEditorPump
    {
        const double EditModeMinIntervalSec = 1.0 / 20.0;
        const int MaxDeviceBindFrames = 180;

        static int s_EditorFrame;
        static int s_DeviceBindFrames;
        static double s_LastPumpTime;
        static bool s_RenderThreadSyncPending;

        static WebViewEditorPump()
        {
            EditorApplication.update += OnEditorUpdate;
            Camera.onPostRender += OnCameraPostRender;
        }

        static void OnEditorUpdate()
        {
            if (Application.isPlaying)
                return;

            if (WebViewPerformanceHub.ActiveCount == 0)
            {
                s_RenderThreadSyncPending = false;
                s_DeviceBindFrames = 0;
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now - s_LastPumpTime < EditModeMinIntervalSec)
                return;
            s_LastPumpTime = now;

            if (!WindowsGpuWebViewNative.IsUnityDeviceReady() &&
                s_DeviceBindFrames < MaxDeviceBindFrames &&
                WindowsGpuWebViewNative.SupportsRenderThreadBind)
            {
                WindowsGpuWebViewNative.TryBindUnityRenderDevice();
                s_DeviceBindFrames++;
            }

            WebViewPerformanceHub.TickAll(++s_EditorFrame);

            // Primary Editor path: main-thread WGC flush (api=12+).
            WindowsGpuWebViewNative.FlushGpuCapturesOnMainThread();
            WebViewPerformanceHub.SyncAllDisplays();

            // Fallback for older plugins: render-thread capture after a SceneView repaint.
            WindowsGpuWebViewNative.IssueGpuCapture();
            s_RenderThreadSyncPending = true;
            RequestEditorRender();
        }

        static void OnCameraPostRender(Camera cam)
        {
            if (Application.isPlaying || !s_RenderThreadSyncPending)
                return;

            WebViewPerformanceHub.SyncAllDisplays();
            s_RenderThreadSyncPending = false;
        }

        static void RequestEditorRender()
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
    }
}
#endif

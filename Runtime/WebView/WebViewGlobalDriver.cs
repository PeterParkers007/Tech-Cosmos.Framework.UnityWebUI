using System.Collections;
using UnityEngine;

namespace UnityWebUI.WebView
{
    /// <summary>
    /// Play-mode pump for all registered WebView backends (LateUpdate, after game logic).
    /// GPU capture runs on the render thread; display sync runs after the frame renders.
    /// </summary>
    sealed class WebViewGlobalDriver : MonoBehaviour
    {
        const int MaxDeviceBindFrames = 180;

        static WebViewGlobalDriver _instance;
        int _deviceBindFrames;
        Coroutine _syncCoroutine;

        public static void Ensure()
        {
            if (_instance != null)
                return;

            var go = new GameObject("UnityWebUI.WebViewGlobalDriver")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<WebViewGlobalDriver>();
        }

        void OnEnable()
        {
            if (_syncCoroutine == null)
                _syncCoroutine = StartCoroutine(SyncAfterRenderLoop());
        }

        void OnDisable()
        {
            if (_syncCoroutine != null)
            {
                StopCoroutine(_syncCoroutine);
                _syncCoroutine = null;
            }
        }

        IEnumerator SyncAfterRenderLoop()
        {
            while (enabled)
            {
                yield return new WaitForEndOfFrame();
                WebViewPerformanceHub.SyncAllDisplays();
            }
        }

        void LateUpdate()
        {
            if (!WindowsGpuWebViewNative.IsUnityDeviceReady() &&
                _deviceBindFrames < MaxDeviceBindFrames &&
                WindowsGpuWebViewNative.SupportsRenderThreadBind)
            {
                WindowsGpuWebViewNative.TryBindUnityRenderDevice();
                _deviceBindFrames++;
            }

            WebViewPerformanceHub.TickAll(Time.frameCount);
            WindowsGpuWebViewNative.IssueGpuCapture();
        }

        void OnDestroy()
        {
            OnDisable();
            if (_instance == this)
                _instance = null;
        }
    }
}

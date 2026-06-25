using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityWebUI.WebView
{
    public static class WindowsGpuWebViewNative
    {
        const string DllName = "UnityWebUI.WebView2Gpu";

        public delegate void MessageCallback(string jsonUtf8, IntPtr userData);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int WebViewGpu_IsSupported();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int WebViewGpu_GetApiVersion();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int WebViewGpu_Create(int width, int height, int transparent);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_Destroy(int handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int WebViewGpu_IsInitialized(int handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_LoadUrl(int handle, string urlUtf8);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_Resize(int handle, int width, int height);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_SetRenderScale(int handle, float scale);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_Tick(int handle, int captureFrame);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr WebViewGpu_GetTexturePointer(int handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int WebViewGpu_GetTextureWidth(int handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int WebViewGpu_GetTextureHeight(int handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int WebViewGpu_HasCpuFrame(int handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int WebViewGpu_HasGpuFrame(int handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int WebViewGpu_CopyCpuFrame(int handle, byte[] dstBgra, int dstBytes, out int outWidth, out int outHeight);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_GetRuntimeStatus(int handle, out int initialized, out int hasGpuTexture, out int hasCpuFrame, out int deviceReady);

        public static bool SupportsPointerMove { get; private set; } = true;
        public static bool SupportsRenderThreadBind { get; private set; } = true;
        public static bool SupportsPluginDiagnostics { get; private set; } = true;

        static IntPtr s_RenderEventFunc = IntPtr.Zero;

        public static void WebViewGpu_GetPluginDiagnostics(out int pluginLoaded, out int renderer, out int hasD3D11, out int deviceReady)
        {
            pluginLoaded = renderer = hasD3D11 = deviceReady = 0;
            if (!SupportsPluginDiagnostics)
                return;

            try
            {
                WebViewGpu_GetPluginDiagnosticsNative(out pluginLoaded, out renderer, out hasD3D11, out deviceReady);
            }
            catch (EntryPointNotFoundException)
            {
                SupportsPluginDiagnostics = false;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebViewGpu_GetPluginDiagnostics")]
        static extern void WebViewGpu_GetPluginDiagnosticsNative(out int pluginLoaded, out int renderer, out int hasD3D11, out int deviceReady);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebViewGpu_GetRenderEventFunc")]
        static extern IntPtr WebViewGpu_GetRenderEventFuncNative();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebViewGpu_FlushGpuCaptures")]
        static extern void WebViewGpu_FlushGpuCapturesNative();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebViewGpu_GetCaptureDiagnostics")]
        static extern void WebViewGpu_GetCaptureDiagnosticsNative(int handle, out int hasSession, out int hasFramePool, out int hasOutput, out int lastGpuCopyStage);

        public static bool SupportsCaptureDiagnostics { get; private set; } = true;
        public static bool SupportsMainThreadGpuFlush { get; private set; } = true;

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_Click(int handle, int x, int y);

        public static bool SupportsPointerPress { get; private set; } = true;

        public static void WebViewGpu_PointerDown(int handle, int x, int y)
        {
            if (!SupportsPointerPress)
            {
                WebViewGpu_Click(handle, x, y);
                return;
            }

            try
            {
                WebViewGpu_PointerDownNative(handle, x, y);
            }
            catch (EntryPointNotFoundException)
            {
                SupportsPointerPress = false;
                WebViewGpu_Click(handle, x, y);
            }
        }

        public static void WebViewGpu_PointerUp(int handle, int x, int y)
        {
            if (!SupportsPointerPress)
                return;

            try
            {
                WebViewGpu_PointerUpNative(handle, x, y);
            }
            catch (EntryPointNotFoundException)
            {
                SupportsPointerPress = false;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebViewGpu_PointerDown")]
        static extern void WebViewGpu_PointerDownNative(int handle, int x, int y);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebViewGpu_PointerUp")]
        static extern void WebViewGpu_PointerUpNative(int handle, int x, int y);

        public static void WebViewGpu_PointerMove(int handle, int x, int y)
        {
            if (!SupportsPointerMove)
                return;

            try
            {
                WebViewGpu_PointerMoveNative(handle, x, y);
            }
            catch (EntryPointNotFoundException)
            {
                SupportsPointerMove = false;
            }
        }

        public static void WebViewGpu_PointerLeave(int handle)
        {
            if (!SupportsPointerMove)
                return;

            try
            {
                WebViewGpu_PointerLeaveNative(handle);
            }
            catch (EntryPointNotFoundException)
            {
                SupportsPointerMove = false;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebViewGpu_PointerMove")]
        static extern void WebViewGpu_PointerMoveNative(int handle, int x, int y);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "WebViewGpu_PointerLeave")]
        static extern void WebViewGpu_PointerLeaveNative(int handle);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_Scroll(int handle, int x, int y, float deltaY);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_SendKey(int handle, int virtualKey, int keyDown);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_ExecuteJavaScript(int handle, string scriptUtf8);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void WebViewGpu_SetMessageCallback(int handle, MessageCallback callback, IntPtr userData);

        public static bool IsDllPresent()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor &&
                Application.platform != RuntimePlatform.WindowsPlayer)
                return false;

            return File.Exists(GetPluginDllPath()) || File.Exists(GetEditorBuiltDllPath());
        }

        public static string GetPluginDllPath()
        {
            var root = UnityWebUIPackagePaths.TryGetPackageRoot();
            if (!string.IsNullOrEmpty(root))
            {
                return Path.GetFullPath(Path.Combine(
                    root, "Plugins", "Windows", "x86_64", "UnityWebUI.WebView2Gpu.dll"));
            }

            return Path.GetFullPath(Path.Combine(
                Application.dataPath, "UnityWebUI", "Plugins", "Windows", "x86_64", "UnityWebUI.WebView2Gpu.dll"));
        }

        static string GetEditorBuiltDllPath() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "WebView2Build", "Native", "x64", "Release", "_out", "UnityWebUI.WebView2Gpu.dll"));

        public static string ProbeDiagnostics()
        {
            var builtPath = GetEditorBuiltDllPath();
            var pluginPath = GetPluginDllPath();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"platform={Application.platform}");
            sb.AppendLine($"graphics={SystemInfo.graphicsDeviceType}");
            sb.AppendLine($"isPlaying={Application.isPlaying}");
            sb.AppendLine($"builtDllExists={File.Exists(builtPath)}");
            sb.AppendLine($"builtDllPath={builtPath}");
            sb.AppendLine($"pluginDllExists={File.Exists(pluginPath)}");
            sb.AppendLine($"pluginDllPath={pluginPath}");
#if UNITY_EDITOR
            sb.AppendLine($"editorPluginEnabled={File.Exists(pluginPath)} (Plugins/Editor must be enabled in .meta)");
#endif
            sb.AppendLine($"tryLoad={TryLoad(out var reason)} reason={reason ?? "(none)"}");
            try
            {
                sb.AppendLine($"apiVersion={WebViewGpu_GetApiVersion()}");
                sb.AppendLine(GetPluginDiagnosticsText());
            }
            catch (Exception ex)
            {
                sb.AppendLine($"apiVersion=error:{ex.Message}");
            }

            return sb.ToString();
        }

        public static string GetPluginDiagnosticsText()
        {
            if (!SupportsPluginDiagnostics)
                return "native diagnostics unavailable (need api=5 plugin)";

            WebViewGpu_GetPluginDiagnostics(out var loaded, out var renderer, out var d3d11, out var device);
            return $"native pluginLoad={loaded} renderer={DescribeRenderer(renderer)} hasD3D11={d3d11} device={device}";
        }

        static string DescribeRenderer(int renderer) =>
            renderer switch
            {
                2 => "D3D11",
                18 => "D3D12",
                21 => "Vulkan",
                -1 => "unknown",
                _ => renderer.ToString()
            };

        static IntPtr GetRenderEventFuncSafe()
        {
            if (!SupportsRenderThreadBind)
                return IntPtr.Zero;

            if (s_RenderEventFunc != IntPtr.Zero)
                return s_RenderEventFunc;

            try
            {
                s_RenderEventFunc = WebViewGpu_GetRenderEventFuncNative();
                return s_RenderEventFunc;
            }
            catch (EntryPointNotFoundException)
            {
                SupportsRenderThreadBind = false;
                return IntPtr.Zero;
            }
        }

        public static bool IsUnityDeviceReady()
        {
            WebViewGpu_GetPluginDiagnostics(out _, out _, out _, out var device);
            return device != 0;
        }

        public static void TryBindUnityRenderDevice()
        {
            if (!SupportsRenderThreadBind)
                return;

            try
            {
                if (WebViewGpu_GetApiVersion() < 5)
                {
                    SupportsRenderThreadBind = false;
                    return;
                }
            }
            catch
            {
                SupportsRenderThreadBind = false;
                return;
            }

            var fn = GetRenderEventFuncSafe();
            if (fn == IntPtr.Zero)
                return;

            GL.IssuePluginEvent(fn, 1);
        }

        public static void IssueGpuCapture()
        {
            if (!SupportsRenderThreadBind)
                return;

            try
            {
                if (WebViewGpu_GetApiVersion() < 8)
                    return;
            }
            catch
            {
                return;
            }

            var fn = GetRenderEventFuncSafe();
            if (fn == IntPtr.Zero)
                return;

            GL.IssuePluginEvent(fn, 2);
        }

        /// <summary>
        /// Editor preview: process pending WGC copies on the main thread when render-thread events do not fire.
        /// </summary>
        public static void FlushGpuCapturesOnMainThread()
        {
            if (!SupportsMainThreadGpuFlush)
            {
                IssueGpuCapture();
                return;
            }

            try
            {
                if (WebViewGpu_GetApiVersion() < 12)
                {
                    IssueGpuCapture();
                    return;
                }

                WebViewGpu_FlushGpuCapturesNative();
            }
            catch (EntryPointNotFoundException)
            {
                SupportsMainThreadGpuFlush = false;
                IssueGpuCapture();
            }
        }

        public static bool TryLoad(out string failureReason)
        {
            failureReason = null;

            if (!IsDllPresent())
            {
                failureReason = "gpu dll missing → build: Window/Unity Web UI/Build Windows GPU Plugin";
                return false;
            }

            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
            {
                failureReason =
                    $"gpu needs D3D11 (current: {SystemInfo.graphicsDeviceType}) → Edit/Project Settings/Player/Windows → Graphics APIs → put Direct3D11 first, remove D3D12";
                return false;
            }

            try
            {
                if (WebViewGpu_GetApiVersion() < 3)
                {
                    failureReason = $"gpu plugin outdated (api={WebViewGpu_GetApiVersion()}) → Build Windows GPU Plugin";
                    return false;
                }

#if !UNITY_EDITOR
                if (WebViewGpu_IsSupported() == 0)
                {
                    failureReason = "gpu native plugin reports D3D11 unavailable (UnityPluginLoad/device not ready)";
                    return false;
                }
#endif

                return true;
            }
            catch (DllNotFoundException ex)
            {
                failureReason = $"gpu dll not loaded: {ex.Message} → enable Editor platform on Plugins/UnityWebUI.WebView2Gpu.dll";
                return false;
            }
            catch (EntryPointNotFoundException ex)
            {
                failureReason = $"gpu plugin outdated: {ex.Message} → Build Windows GPU Plugin";
                return false;
            }
        }

        public static bool TryLoad() => TryLoad(out _);

        public static string GetRuntimeStatus(int handle)
        {
            if (handle <= 0)
                return "handle=invalid";

            WebViewGpu_GetRuntimeStatus(handle, out var init, out var gpu, out var cpu, out var device);
            var api = 0;
            try
            {
                api = WebViewGpu_GetApiVersion();
            }
            catch
            {
                // ignored
            }

            var capture = GetCaptureDiagnosticsText(handle);
            return $"init={init} gpuTex={gpu} cpuFrame={cpu} device={device} api={api} gpuFrame={WebViewGpu_HasGpuFrame(handle)}{capture}";
        }

        public static string GetCaptureDiagnosticsText(int handle)
        {
            if (handle <= 0 || !SupportsCaptureDiagnostics)
                return string.Empty;

            try
            {
                if (WebViewGpu_GetApiVersion() < 13)
                    return string.Empty;

                WebViewGpu_GetCaptureDiagnosticsNative(
                    handle, out var hasSession, out var hasPool, out var hasOutput, out var stage);
                return $" cap=sess:{hasSession} pool:{hasPool} out:{hasOutput} stage:{stage}";
            }
            catch (EntryPointNotFoundException)
            {
                SupportsCaptureDiagnostics = false;
                return string.Empty;
            }
        }
    }
}

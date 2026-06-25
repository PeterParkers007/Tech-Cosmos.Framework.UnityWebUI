#pragma once

#ifdef UNITYWEBUI_WEBVIEW2GPU_EXPORTS
#define WEBVIEWGPU_API __declspec(dllexport)
#else
#define WEBVIEWGPU_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*WebViewGpu_MessageCallback)(const char* jsonUtf8, void* userData);
typedef void (*WebViewGpu_RenderEventFn)(int eventId);

WEBVIEWGPU_API int WebViewGpu_IsSupported();
WEBVIEWGPU_API int WebViewGpu_GetApiVersion();
WEBVIEWGPU_API int WebViewGpu_Create(int width, int height, int transparent);
WEBVIEWGPU_API void WebViewGpu_Destroy(int handle);
WEBVIEWGPU_API int WebViewGpu_IsInitialized(int handle);

WEBVIEWGPU_API void WebViewGpu_LoadUrl(int handle, const char* urlUtf8);
WEBVIEWGPU_API void WebViewGpu_Resize(int handle, int width, int height);
WEBVIEWGPU_API void WebViewGpu_SetRenderScale(int handle, float scale);
WEBVIEWGPU_API void WebViewGpu_Tick(int handle, int captureFrame);

WEBVIEWGPU_API void* WebViewGpu_GetTexturePointer(int handle);
WEBVIEWGPU_API int WebViewGpu_GetTextureWidth(int handle);
WEBVIEWGPU_API int WebViewGpu_GetTextureHeight(int handle);
WEBVIEWGPU_API int WebViewGpu_GetTextureFormatBgra();

WEBVIEWGPU_API int WebViewGpu_HasCpuFrame(int handle);
WEBVIEWGPU_API int WebViewGpu_HasGpuFrame(int handle);
WEBVIEWGPU_API int WebViewGpu_CopyCpuFrame(int handle, unsigned char* dstBgra, int dstBytes, int* outWidth, int* outHeight);
WEBVIEWGPU_API void WebViewGpu_GetRuntimeStatus(int handle, int* outInitialized, int* outHasGpuTexture, int* outHasCpuFrame, int* outDeviceReady);

WEBVIEWGPU_API void WebViewGpu_GetPluginDiagnostics(int* outPluginLoaded, int* outRenderer, int* outHasD3D11, int* outDeviceReady);
WEBVIEWGPU_API WebViewGpu_RenderEventFn WebViewGpu_GetRenderEventFunc();
WEBVIEWGPU_API void WebViewGpu_FlushGpuCaptures();
WEBVIEWGPU_API void WebViewGpu_GetCaptureDiagnostics(int handle, int* outHasSession, int* outHasFramePool, int* outHasOutput, int* outLastGpuCopyStage);

WEBVIEWGPU_API void WebViewGpu_Click(int handle, int x, int y);
WEBVIEWGPU_API void WebViewGpu_PointerDown(int handle, int x, int y);
WEBVIEWGPU_API void WebViewGpu_PointerUp(int handle, int x, int y);
WEBVIEWGPU_API void WebViewGpu_PointerMove(int handle, int x, int y);
WEBVIEWGPU_API void WebViewGpu_PointerLeave(int handle);
WEBVIEWGPU_API void WebViewGpu_Scroll(int handle, int x, int y, float deltaY);
WEBVIEWGPU_API void WebViewGpu_SendKey(int handle, int virtualKey, int keyDown);

WEBVIEWGPU_API void WebViewGpu_ExecuteJavaScript(int handle, const char* scriptUtf8);
WEBVIEWGPU_API void WebViewGpu_SetMessageCallback(int handle, WebViewGpu_MessageCallback callback, void* userData);
WEBVIEWGPU_API void WebViewGpu_PostMessageToPage(int handle, const char* jsonUtf8);

#ifdef __cplusplus
}
#endif

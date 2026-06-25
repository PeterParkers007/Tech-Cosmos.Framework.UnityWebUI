#pragma once

#include "IUnityInterface.h"

typedef enum UnityGfxRenderer
{
    kUnityGfxRendererD3D11 = 2,
    kUnityGfxRendererNull = 4,
    kUnityGfxRendererOpenGLES30 = 11,
    kUnityGfxRendererMetal = 16,
    kUnityGfxRendererOpenGLCore = 17,
    kUnityGfxRendererD3D12 = 18,
    kUnityGfxRendererVulkan = 21,
} UnityGfxRenderer;

typedef enum UnityGfxDeviceEventType
{
    kUnityGfxDeviceEventInitialize = 0,
    kUnityGfxDeviceEventShutdown = 1,
    kUnityGfxDeviceEventBeforeReset = 2,
    kUnityGfxDeviceEventAfterReset = 3,
} UnityGfxDeviceEventType;

typedef void (UNITY_INTERFACE_API* IUnityGraphicsDeviceEventCallback)(UnityGfxDeviceEventType eventType);

UNITY_DECLARE_INTERFACE(IUnityGraphics)
{
    UnityGfxRenderer (UNITY_INTERFACE_API* GetRenderer)();
    void (UNITY_INTERFACE_API* RegisterDeviceEventCallback)(IUnityGraphicsDeviceEventCallback callback);
    void (UNITY_INTERFACE_API* UnregisterDeviceEventCallback)(IUnityGraphicsDeviceEventCallback callback);
    int (UNITY_INTERFACE_API* ReserveEventIDRange)(int count);
};
UNITY_REGISTER_INTERFACE_GUID(0x7CBA0A9CA4DDB544ULL, 0x8C5AD4926EB17B11ULL, IUnityGraphics)

typedef void (UNITY_INTERFACE_API* UnityRenderingEvent)(int eventId);

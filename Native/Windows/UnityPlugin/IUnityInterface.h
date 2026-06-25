#pragma once

#include <stddef.h>

#if defined(WIN32) || defined(_WIN32) || defined(__WIN32__) || defined(_WIN64) || defined(WINAPI_FAMILY)
#define UNITY_INTERFACE_API __stdcall
#define UNITY_INTERFACE_EXPORT __declspec(dllexport)
#else
#define UNITY_INTERFACE_API
#define UNITY_INTERFACE_EXPORT __attribute__((visibility("default")))
#endif

struct UnityInterfaceGUID
{
#ifdef __cplusplus
    UnityInterfaceGUID(unsigned long long high, unsigned long long low)
        : m_GUIDHigh(high), m_GUIDLow(low) {}
#endif
    unsigned long long m_GUIDHigh;
    unsigned long long m_GUIDLow;
};

#ifdef __cplusplus
#define UNITY_DECLARE_INTERFACE(NAME) struct NAME : IUnityInterface

template<typename TYPE>
inline const UnityInterfaceGUID GetUnityInterfaceGUID();

#define UNITY_REGISTER_INTERFACE_GUID(HASHH, HASHL, TYPE)              \
    template<>                                                         \
    inline const UnityInterfaceGUID GetUnityInterfaceGUID<TYPE>()      \
    {                                                                  \
        return UnityInterfaceGUID(HASHH, HASHL);                     \
    }

#define UNITY_GET_INTERFACE_GUID(TYPE) GetUnityInterfaceGUID<TYPE>()
#else
#define UNITY_DECLARE_INTERFACE(NAME) \
    typedef struct NAME NAME;         \
    struct NAME

#define UNITY_REGISTER_INTERFACE_GUID(HASHH, HASHL, TYPE) \
    static const UnityInterfaceGUID TYPE##_GUID = {HASHH, HASHL};

#define UNITY_GET_INTERFACE_GUID(TYPE) TYPE##_GUID
#endif

#define UNITY_GET_INTERFACE(INTERFACES, TYPE) \
    (TYPE*)INTERFACES->GetInterfaceSplit(UNITY_GET_INTERFACE_GUID(TYPE).m_GUIDHigh, UNITY_GET_INTERFACE_GUID(TYPE).m_GUIDLow)

#ifdef __cplusplus
struct IUnityInterface {};
#else
typedef void IUnityInterface;
#endif

typedef struct IUnityInterfaces
{
    IUnityInterface* (UNITY_INTERFACE_API* GetInterface)(UnityInterfaceGUID guid);
    void (UNITY_INTERFACE_API* RegisterInterface)(UnityInterfaceGUID guid, IUnityInterface* ptr);
    IUnityInterface* (UNITY_INTERFACE_API* GetInterfaceSplit)(unsigned long long guidHigh, unsigned long long guidLow);
    void (UNITY_INTERFACE_API* RegisterInterfaceSplit)(unsigned long long guidHigh, unsigned long long guidLow, IUnityInterface* ptr);

#ifdef __cplusplus
    template<typename INTERFACE>
    INTERFACE* Get()
    {
        return static_cast<INTERFACE*>(GetInterface(GetUnityInterfaceGUID<INTERFACE>()));
    }
#endif
} IUnityInterfaces;

#ifdef __cplusplus
extern "C" {
#endif

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces);
void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload();

#ifdef __cplusplus
}
#endif

struct RenderSurfaceBase;
typedef struct RenderSurfaceBase* UnityRenderBuffer;
typedef unsigned int UnityTextureID;

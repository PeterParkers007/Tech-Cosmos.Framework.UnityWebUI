#include <windows.h>
#include <objidl.h>
#include <wrl/client.h>
#include <wrl/event.h>
#include <WebView2.h>
#include <d3d11.h>
#include <dxgi.h>
#include <windowsx.h>
#include <shlwapi.h>
#include <string>
#include <vector>
#include <unordered_map>
#include <mutex>
#include <atomic>
#include <memory>
#include <functional>
#include <algorithm>

#include "../include/WebViewGpuApi.h"
#include "../../UnityPlugin/IUnityInterface.h"
#include "../../UnityPlugin/IUnityGraphics.h"
#include "../../UnityPlugin/IUnityGraphicsD3D11.h"

#include <winrt/base.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>

#include <Windows.Graphics.Capture.Interop.h>
#include <Windows.Graphics.DirectX.Direct3D11.interop.h>

#pragma comment(lib, "d3d11.lib")
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "windowsapp.lib")

using Microsoft::WRL::ComPtr;

namespace wgc = winrt::Windows::Graphics::Capture;
namespace wgd = winrt::Windows::Graphics::DirectX;
namespace wgd3d = winrt::Windows::Graphics::DirectX::Direct3D11;

static IUnityInterfaces* g_UnityInterfaces = nullptr;
static IUnityGraphics* g_Graphics = nullptr;
static IUnityGraphicsD3D11* g_GraphicsD3D11 = nullptr;
static ID3D11Device* g_UnityDevice = nullptr;
static bool g_UnityPluginLoadCalled = false;
static bool g_DeviceCallbackRegistered = false;
static std::mutex g_Mutex;
static std::atomic<bool> g_WinRtInitialized{ false };
static std::atomic<bool> g_EnvironmentReady{ false };
static ComPtr<ICoreWebView2Environment> g_Environment;
static ATOM g_WindowClassAtom = 0;
static HINSTANCE g_Module = nullptr;

static const wchar_t* kHostClassName = L"UnityWebUI.WebView2Gpu.Host";
static const wchar_t* kBridgeScript = LR"JS(
(function(){
  if(window.__unityWebUiGpuBridge)return;
  window.__unityWebUiGpuBridge=true;
  function post(payload){
    var json=JSON.stringify(payload);
    if(window.chrome&&window.chrome.webview&&window.chrome.webview.postMessage){
      window.chrome.webview.postMessage(json);
    }
  }
  function stripTags(text){return(text||'').replace(/<[^>]+>/g,' ').replace(/\s+/g,' ').trim();}
  function isCjk(ch){var c=ch.charCodeAt(0);return c>=0x4e00&&c<=0x9fff;}
  function slugFromText(text){
    text=stripTags(text);if(!text)return '';
    var slug='',sep=false;
    for(var i=0;i<text.length;i++){
      var ch=text.charAt(i),code=text.charCodeAt(i);
      var word=(code>=48&&code<=57)||(code>=65&&code<=90)||(code>=97&&code<=122)||isCjk(ch);
      if(word){slug+=ch;sep=false;}
      else if(!sep&&slug.length){slug+='-';sep=true;}
    }
    slug=slug.replace(/^-+|-+$/g,'');
    if(slug.length>32)slug=slug.substring(0,32).replace(/-+$/g,'');
    return slug;
  }
  function resolveButtonActionIds(buttons){
    var slugCounts={},ids=new Array(buttons.length);
    for(var i=0;i<buttons.length;i++){
      var btn=buttons[i],explicit=btn.getAttribute('data-unity-action');
      if(explicit){ids[i]=explicit.trim();continue;}
      if(btn.id){ids[i]=btn.id.trim();continue;}
      var slug=slugFromText(btn.textContent||'');
      if(slug){
        if(!slugCounts[slug]){slugCounts[slug]=1;ids[i]=slug;}
        else{slugCounts[slug]++;ids[i]=slug+'-'+slugCounts[slug];}
        continue;
      }
      ids[i]='button-'+i;
    }
    return ids;
  }
  function resolveButtonActionId(btn){
    var buttons=Array.prototype.slice.call(document.querySelectorAll('button'));
    var index=buttons.indexOf(btn);
    if(index<0)return 'button';
    return resolveButtonActionIds(buttons)[index];
  }
  window.__unityWebUiEmitActionForElement=function(startEl){
    if(!startEl||!startEl.closest)return false;
    var actionEl=startEl.closest('[data-unity-action]');
    if(actionEl){
      post({type:'action',id:actionEl.getAttribute('data-unity-action')});
      return true;
    }
    var button=startEl.closest('button');
    if(button){
      post({type:'action',id:resolveButtonActionId(button)});
      return true;
    }
    return false;
  };
  window.__unityWebUiEmitActionAtPoint=function(x,y){
    var el=document.elementFromPoint(x,y);
    if(!el)return false;
    el.dispatchEvent(new MouseEvent('click',{bubbles:true,cancelable:true,view:window,clientX:x,clientY:y,button:0}));
    return window.__unityWebUiEmitActionForElement(el);
  };
  document.addEventListener('click',function(ev){
    if(window.__unityWebUiEmitActionForElement(ev.target)){ev.preventDefault();return;}
    var link=ev.target.closest('a[href]');
    if(!link||link.hasAttribute('data-unity-action'))return;
    var href=link.getAttribute('href')||'';
    if(href.charAt(0)==='#')return;
    ev.preventDefault();
    post({type:'navigate',href:href});
  },true);
})();
)JS";

struct WebViewGpuInstance
{
    int handle = 0;
    int width = 1280;
    int height = 720;
    float renderScale = 1.f;
    bool transparent = false;
    bool initialized = false;

    HWND hostHwnd = nullptr;
    ComPtr<ICoreWebView2Controller> controller;
    ComPtr<ICoreWebView2> webview;

    wgc::Direct3D11CaptureFramePool framePool{ nullptr };
    wgc::GraphicsCaptureSession captureSession{ nullptr };
    wgc::GraphicsCaptureItem captureItem{ nullptr };
    winrt::Windows::Graphics::SizeInt32 captureSize{};

    ComPtr<ID3D11Device> captureDevice;
    ComPtr<ID3D11DeviceContext> captureContext;
    ComPtr<ID3D11Texture2D> outputTexture;
    ComPtr<ID3D11ShaderResourceView> outputSrv;
    ComPtr<ID3D11Texture2D> captureStagingTexture;
    int textureWidth = 0;
    int textureHeight = 0;
    bool outputTextureDirty = true;
    bool gpuFrameValid = false;
    std::atomic<bool> pendingGpuCapture{ false };

    WebViewGpu_MessageCallback messageCallback = nullptr;
    void* messageUserData = nullptr;

    EventRegistrationToken messageToken{};
    std::string pendingUrlUtf8;

    std::vector<uint8_t> cpuFrameBgra;
    int cpuFrameWidth = 0;
    int cpuFrameHeight = 0;
    bool cpuFrameValid = false;
    int lastGpuCopyStage = 0;
};

static std::unordered_map<int, std::unique_ptr<WebViewGpuInstance>> g_Instances;
static int g_NextHandle = 1;

static void RecreateOutputTexture(WebViewGpuInstance& inst);
static void SetupCapture(WebViewGpuInstance& inst);
static void ResizeComposition(WebViewGpuInstance& inst);
static bool CopyLatestCaptureFrame(WebViewGpuInstance& inst);
static void ProcessPendingGpuCaptures();

static void EnsureWinRt()
{
    if (g_WinRtInitialized.load())
        return;

    try
    {
        winrt::init_apartment(winrt::apartment_type::multi_threaded);
    }
    catch (...)
    {
        // Apartment may already be initialized by the host.
    }

    g_WinRtInitialized.store(true);
}

static void RegisterHostWindowClass()
{
    if (g_WindowClassAtom != 0)
        return;

    WNDCLASSEXW wc{};
    wc.cbSize = sizeof(wc);
    wc.lpfnWndProc = DefWindowProcW;
    wc.hInstance = g_Module;
    wc.lpszClassName = kHostClassName;
    g_WindowClassAtom = RegisterClassExW(&wc);
}

static HWND CreateHostWindow(int width, int height)
{
    RegisterHostWindowClass();
    HWND hwnd = CreateWindowExW(
        WS_EX_TOOLWINDOW,
        kHostClassName,
        L"UnityWebUI WebView Host",
        WS_POPUP,
        -32000, -32000, width, height,
        nullptr, nullptr, g_Module, nullptr);
    if (hwnd)
        ShowWindow(hwnd, SW_SHOWNOACTIVATE);
    return hwnd;
}

static void PumpHostMessages()
{
    MSG msg{};
    while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }
}

static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType);

static void EnsureUnityInterfaces()
{
    if (!g_UnityInterfaces)
        return;

    if (!g_Graphics)
    {
        g_Graphics = g_UnityInterfaces->Get<IUnityGraphics>();
        if (g_Graphics && !g_DeviceCallbackRegistered)
        {
            g_Graphics->RegisterDeviceEventCallback(OnGraphicsDeviceEvent);
            g_DeviceCallbackRegistered = true;
        }
    }

    if (!g_GraphicsD3D11)
        g_GraphicsD3D11 = g_UnityInterfaces->Get<IUnityGraphicsD3D11>();
}

static void EnsureUnityDevice()
{
    EnsureUnityInterfaces();
    if (!g_UnityDevice && g_GraphicsD3D11)
        g_UnityDevice = g_GraphicsD3D11->GetDevice();
}

static void TryAcquireUnityDevice()
{
    EnsureUnityInterfaces();
    EnsureUnityDevice();
    if (g_UnityDevice || !g_Graphics || !g_GraphicsD3D11)
        return;

    if (g_Graphics->GetRenderer() != kUnityGfxRendererD3D11)
        return;

    g_UnityDevice = g_GraphicsD3D11->GetDevice();
}

static void UNITY_INTERFACE_API OnRenderEvent(int eventId)
{
    if (eventId == 1)
    {
        TryAcquireUnityDevice();
        return;
    }

    if (eventId == 2)
        ProcessPendingGpuCaptures();
}

static void NavigateInstanceUtf8(WebViewGpuInstance& inst, const char* urlUtf8)
{
    if (!inst.webview || !urlUtf8)
        return;

    const int len = MultiByteToWideChar(CP_UTF8, 0, urlUtf8, -1, nullptr, 0);
    if (len <= 1)
        return;

    std::wstring url(static_cast<size_t>(len - 1), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, urlUtf8, -1, url.data(), len);
    inst.webview->Navigate(url.c_str());
}

static void EnsureGpuResources(WebViewGpuInstance& inst)
{
    if (inst.initialized && inst.controller && !inst.captureSession)
        SetupCapture(inst);
}

static void EnsureCaptureStaging(WebViewGpuInstance& inst, const D3D11_TEXTURE2D_DESC& srcDesc, ID3D11Device* srcDevice)
{
    if (!srcDevice)
        return;

    if (inst.captureStagingTexture)
    {
        D3D11_TEXTURE2D_DESC existing{};
        inst.captureStagingTexture->GetDesc(&existing);
        if (existing.Width == srcDesc.Width && existing.Height == srcDesc.Height)
            return;
        inst.captureStagingTexture.Reset();
    }

    D3D11_TEXTURE2D_DESC stagingDesc = srcDesc;
    stagingDesc.BindFlags = 0;
    stagingDesc.Usage = D3D11_USAGE_STAGING;
    stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    stagingDesc.MiscFlags = 0;
    srcDevice->CreateTexture2D(&stagingDesc, nullptr, &inst.captureStagingTexture);
}

static void RecreateOutputTexture(WebViewGpuInstance& inst)
{
    if (!g_UnityDevice)
        return;

    const int w = std::max(1, static_cast<int>(inst.width * inst.renderScale));
    const int h = std::max(1, static_cast<int>(inst.height * inst.renderScale));

    if (!inst.outputTextureDirty && inst.outputTexture && inst.outputSrv &&
        inst.textureWidth == w && inst.textureHeight == h)
        return;

    inst.outputSrv.Reset();
    inst.outputTexture.Reset();
    inst.textureWidth = w;
    inst.textureHeight = h;
    inst.outputTextureDirty = false;
    inst.gpuFrameValid = false;

    D3D11_TEXTURE2D_DESC desc{};
    desc.Width = static_cast<UINT>(w);
    desc.Height = static_cast<UINT>(h);
    desc.MipLevels = 1;
    desc.ArraySize = 1;
    desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    desc.SampleDesc.Count = 1;
    desc.SampleDesc.Quality = 0;
    desc.Usage = D3D11_USAGE_DEFAULT;
    desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
    desc.CPUAccessFlags = 0;
    desc.MiscFlags = 0;

    if (FAILED(g_UnityDevice->CreateTexture2D(&desc, nullptr, &inst.outputTexture)))
    {
        inst.textureWidth = 0;
        inst.textureHeight = 0;
        return;
    }

    D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc{};
    srvDesc.Format = desc.Format;
    srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
    srvDesc.Texture2D.MipLevels = 1;
    srvDesc.Texture2D.MostDetailedMip = 0;

    if (FAILED(g_UnityDevice->CreateShaderResourceView(inst.outputTexture.Get(), &srvDesc, &inst.outputSrv)))
    {
        inst.outputTexture.Reset();
        inst.textureWidth = 0;
        inst.textureHeight = 0;
    }
}

static ComPtr<ID3D11Device> CreateWinRtD3DDevice(ID3D11Device* unityDevice)
{
    ComPtr<IDXGIDevice> dxgiDevice;
    if (FAILED(unityDevice->QueryInterface(IID_PPV_ARGS(&dxgiDevice))))
        return nullptr;

    winrt::com_ptr<IInspectable> inspectable;
    if (FAILED(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.Get(), inspectable.put())))
        return nullptr;

    ComPtr<ID3D11Device> device;
    if (FAILED(inspectable->QueryInterface(IID_PPV_ARGS(&device))))
        return nullptr;
    return device;
}

static void SetupCapture(WebViewGpuInstance& inst)
{
    if (!inst.hostHwnd || !g_UnityDevice)
        return;

    try
    {
        if (inst.captureSession)
        {
            inst.captureSession.Close();
            inst.captureSession = nullptr;
        }
        if (inst.framePool)
        {
            inst.framePool.Close();
            inst.framePool = nullptr;
        }

        wgd3d::IDirect3DDevice directDevice{ nullptr };
        ComPtr<ID3D11Device> winRtDevice = CreateWinRtD3DDevice(g_UnityDevice);
        if (!winRtDevice)
            return;

        inst.captureDevice = winRtDevice;
        inst.captureDevice->GetImmediateContext(&inst.captureContext);

        ComPtr<IDXGIDevice> dxgiDevice;
        if (SUCCEEDED(winRtDevice->QueryInterface(IID_PPV_ARGS(&dxgiDevice))))
        {
            winrt::com_ptr<IInspectable> inspectable;
            if (SUCCEEDED(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.Get(), inspectable.put())))
                directDevice = inspectable.as<wgd3d::IDirect3DDevice>();
        }

        if (!directDevice)
            return;

        const int w = std::max(1, static_cast<int>(inst.width * inst.renderScale));
        const int h = std::max(1, static_cast<int>(inst.height * inst.renderScale));
        inst.captureSize = winrt::Windows::Graphics::SizeInt32{ w, h };

        auto factory = winrt::get_activation_factory<wgc::GraphicsCaptureItem>();
        winrt::com_ptr<IGraphicsCaptureItemInterop> interop;
        factory.as(interop);
        winrt::com_ptr<ABI::Windows::Graphics::Capture::IGraphicsCaptureItem> nativeItem;
        if (FAILED(interop->CreateForWindow(
                inst.hostHwnd,
                winrt::guid_of<ABI::Windows::Graphics::Capture::IGraphicsCaptureItem>(),
                winrt::put_abi(nativeItem))))
            return;

        inst.captureItem = nativeItem.as<wgc::GraphicsCaptureItem>();

        inst.framePool = wgc::Direct3D11CaptureFramePool::CreateFreeThreaded(
            directDevice,
            wgd::DirectXPixelFormat::B8G8R8A8UIntNormalized,
            2,
            inst.captureSize);

        inst.captureSession = inst.framePool.CreateCaptureSession(inst.captureItem);
        inst.captureSession.IsBorderRequired(false);
        inst.captureSession.StartCapture();
    }
    catch (...)
    {
        inst.captureSession = nullptr;
        inst.framePool = nullptr;
    }
}

static void ResizeComposition(WebViewGpuInstance& inst)
{
    if (!inst.controller)
        return;

    const int w = std::max(1, static_cast<int>(inst.width * inst.renderScale));
    const int h = std::max(1, static_cast<int>(inst.height * inst.renderScale));

    RECT bounds{ 0, 0, w, h };
    inst.controller->put_Bounds(bounds);

    if (inst.captureSession)
    {
        inst.captureSession.Close();
        inst.captureSession = nullptr;
    }
    if (inst.framePool)
    {
        inst.framePool.Close();
        inst.framePool = nullptr;
    }

    SetupCapture(inst);
    inst.outputTextureDirty = true;
}

static void EnsureEnvironment(HWND parent, std::function<void(HRESULT)> onReady)
{
    if (g_EnvironmentReady)
    {
        onReady(S_OK);
        return;
    }

    CreateCoreWebView2EnvironmentWithOptions(
        nullptr, nullptr, nullptr,
        Microsoft::WRL::Callback<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>(
            [parent, onReady](HRESULT result, ICoreWebView2Environment* env) -> HRESULT
            {
                if (FAILED(result))
                {
                    onReady(result);
                    return result;
                }
                g_Environment = env;
                g_EnvironmentReady = true;
                onReady(S_OK);
                return S_OK;
            }).Get());
}

static void FinishWebViewInit(WebViewGpuInstance& inst)
{
    if (!inst.webview)
        return;

    ComPtr<ICoreWebView2Settings> settings;
    if (SUCCEEDED(inst.webview->get_Settings(&settings)))
    {
        settings->put_IsScriptEnabled(TRUE);
        settings->put_AreDefaultScriptDialogsEnabled(TRUE);
        settings->put_IsWebMessageEnabled(TRUE);
    }

    if (inst.transparent)
    {
        ComPtr<ICoreWebView2Controller2> controller2;
        if (SUCCEEDED(inst.controller.As(&controller2)) && controller2)
        {
            COREWEBVIEW2_COLOR color{ 0, 0, 0, 0 };
            controller2->put_DefaultBackgroundColor(color);
        }
    }

    inst.webview->AddScriptToExecuteOnDocumentCreated(kBridgeScript, nullptr);
    inst.webview->add_WebMessageReceived(
        Microsoft::WRL::Callback<ICoreWebView2WebMessageReceivedEventHandler>(
            [&inst](ICoreWebView2*, ICoreWebView2WebMessageReceivedEventArgs* args) -> HRESULT
            {
                LPWSTR messageRaw = nullptr;
                if (FAILED(args->TryGetWebMessageAsString(&messageRaw)) || !messageRaw)
                    return S_OK;

                if (inst.messageCallback)
                {
                    const int len = WideCharToMultiByte(CP_UTF8, 0, messageRaw, -1, nullptr, 0, nullptr, nullptr);
                    if (len > 1)
                    {
                        std::string utf8(static_cast<size_t>(len - 1), '\0');
                        WideCharToMultiByte(CP_UTF8, 0, messageRaw, -1, utf8.data(), len, nullptr, nullptr);
                        inst.messageCallback(utf8.c_str(), inst.messageUserData);
                    }
                }
                CoTaskMemFree(messageRaw);
                return S_OK;
            }).Get(),
        &inst.messageToken);

    inst.initialized = true;
    ResizeComposition(inst);

    if (!inst.pendingUrlUtf8.empty())
    {
        const std::string url = inst.pendingUrlUtf8;
        inst.pendingUrlUtf8.clear();
        NavigateInstanceUtf8(inst, url.c_str());
    }
}

static void BeginCreateWebView(WebViewGpuInstance& inst)
{
    EnsureEnvironment(inst.hostHwnd, [&inst](HRESULT envResult)
    {
        if (FAILED(envResult) || !g_Environment)
            return;

        g_Environment->CreateCoreWebView2Controller(
            inst.hostHwnd,
            Microsoft::WRL::Callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>(
                [&inst](HRESULT result, ICoreWebView2Controller* controller) -> HRESULT
                {
                    if (FAILED(result) || !controller)
                        return result;

                    inst.controller = controller;
                    controller->get_CoreWebView2(&inst.webview);
                    controller->put_IsVisible(TRUE);
                    FinishWebViewInit(inst);
                    return S_OK;
                }).Get());
    });
}

static bool CopyLatestCaptureFrame(WebViewGpuInstance& inst)
{
    inst.lastGpuCopyStage = 1;
    if (!inst.framePool || !g_UnityDevice)
    {
        inst.lastGpuCopyStage = inst.outputTexture ? 2 : 3;
        return false;
    }

    if (!inst.outputTexture || !inst.outputSrv)
        RecreateOutputTexture(inst);

    if (!inst.outputTexture || !inst.outputSrv)
    {
        inst.lastGpuCopyStage = 3;
        return false;
    }

    ComPtr<ID3D11DeviceContext> unityContext;
    g_UnityDevice->GetImmediateContext(&unityContext);
    if (!unityContext)
    {
        inst.lastGpuCopyStage = 4;
        return false;
    }

    wgc::Direct3D11CaptureFrame latestFrame{ nullptr };
    while (true)
    {
        auto frame = inst.framePool.TryGetNextFrame();
        if (!frame)
            break;
        latestFrame = frame;
    }

    if (!latestFrame)
    {
        inst.lastGpuCopyStage = 5;
        return false;
    }

    inst.lastGpuCopyStage = 6;
    auto surface = latestFrame.Surface();
    ComPtr<Windows::Graphics::DirectX::Direct3D11::IDirect3DDxgiInterfaceAccess> access;
    if (FAILED(winrt::get_unknown(surface)->QueryInterface(IID_PPV_ARGS(&access))))
    {
        inst.lastGpuCopyStage = 7;
        return false;
    }

    ComPtr<ID3D11Texture2D> srcTexture;
    if (FAILED(access->GetInterface(IID_PPV_ARGS(&srcTexture))))
    {
        inst.lastGpuCopyStage = 8;
        return false;
    }

    D3D11_TEXTURE2D_DESC srcDesc{};
    srcTexture->GetDesc(&srcDesc);

    if (srcDesc.Width != static_cast<UINT>(inst.textureWidth) ||
        srcDesc.Height != static_cast<UINT>(inst.textureHeight))
    {
        inst.textureWidth = static_cast<int>(srcDesc.Width);
        inst.textureHeight = static_cast<int>(srcDesc.Height);
        inst.outputTextureDirty = true;
        inst.outputSrv.Reset();
        inst.outputTexture.Reset();
        inst.outputTextureDirty = false;

        D3D11_TEXTURE2D_DESC desc = srcDesc;
        desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.CPUAccessFlags = 0;
        desc.MiscFlags = 0;

        if (FAILED(g_UnityDevice->CreateTexture2D(&desc, nullptr, &inst.outputTexture)))
        {
            inst.lastGpuCopyStage = 9;
            return false;
        }

        D3D11_SHADER_RESOURCE_VIEW_DESC srvDesc{};
        srvDesc.Format = desc.Format;
        srvDesc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
        srvDesc.Texture2D.MipLevels = 1;
        srvDesc.Texture2D.MostDetailedMip = 0;
        if (FAILED(g_UnityDevice->CreateShaderResourceView(inst.outputTexture.Get(), &srvDesc, &inst.outputSrv)))
        {
            inst.outputTexture.Reset();
            inst.lastGpuCopyStage = 10;
            return false;
        }
        inst.gpuFrameValid = false;
    }

    ComPtr<ID3D11Device> srcDevice;
    srcTexture->GetDevice(&srcDevice);
    if (srcDevice.Get() == g_UnityDevice)
    {
        unityContext->CopyResource(inst.outputTexture.Get(), srcTexture.Get());
        inst.gpuFrameValid = true;
        inst.lastGpuCopyStage = 0;
        return true;
    }

    if (!inst.captureContext)
        inst.captureDevice->GetImmediateContext(&inst.captureContext);
    if (!inst.captureContext)
    {
        inst.lastGpuCopyStage = 11;
        return false;
    }

    EnsureCaptureStaging(inst, srcDesc, srcDevice.Get());
    if (!inst.captureStagingTexture)
    {
        inst.lastGpuCopyStage = 12;
        return false;
    }

    inst.captureContext->CopyResource(inst.captureStagingTexture.Get(), srcTexture.Get());

    D3D11_MAPPED_SUBRESOURCE mapped{};
    if (FAILED(inst.captureContext->Map(inst.captureStagingTexture.Get(), 0, D3D11_MAP_READ, 0, &mapped)))
    {
        inst.lastGpuCopyStage = 13;
        return false;
    }

    unityContext->UpdateSubresource(
        inst.outputTexture.Get(),
        0,
        nullptr,
        mapped.pData,
        mapped.RowPitch,
        0);
    inst.captureContext->Unmap(inst.captureStagingTexture.Get(), 0);
    inst.gpuFrameValid = true;
    inst.lastGpuCopyStage = 0;
    return true;
}

static bool GpuCaptureReady(const WebViewGpuInstance& inst)
{
    return g_UnityDevice && inst.captureSession;
}

static void ProcessPendingGpuCaptures()
{
    TryAcquireUnityDevice();
    if (!g_UnityDevice)
        return;

    std::lock_guard<std::mutex> lock(g_Mutex);
    for (auto& pair : g_Instances)
    {
        auto& inst = *pair.second;
        EnsureGpuResources(inst);
        if (!GpuCaptureReady(inst))
            continue;

        if (!inst.pendingGpuCapture.load())
            inst.pendingGpuCapture = true;

        inst.pendingGpuCapture = false;
        RecreateOutputTexture(inst);
        if (CopyLatestCaptureFrame(inst))
            inst.cpuFrameValid = false;
    }
}

static bool CaptureHostWindowCpu(WebViewGpuInstance& inst)
{
    if (!inst.hostHwnd || !inst.initialized)
        return false;

    const int w = std::max(1, static_cast<int>(inst.width * inst.renderScale));
    const int h = std::max(1, static_cast<int>(inst.height * inst.renderScale));

    if (inst.controller)
    {
        RECT bounds{ 0, 0, w, h };
        inst.controller->put_Bounds(bounds);
    }

    RedrawWindow(inst.hostHwnd, nullptr, nullptr, RDW_INVALIDATE | RDW_UPDATENOW | RDW_ALLCHILDREN);
    UpdateWindow(inst.hostHwnd);
    PumpHostMessages();

    HDC windowDc = GetDC(inst.hostHwnd);
    if (!windowDc)
        return false;

    HDC memDc = CreateCompatibleDC(windowDc);
    if (!memDc)
    {
        ReleaseDC(inst.hostHwnd, windowDc);
        return false;
    }

    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = w;
    bmi.bmiHeader.biHeight = -h;
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    void* bits = nullptr;
    HBITMAP dib = CreateDIBSection(memDc, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0);
    if (!dib || !bits)
    {
        DeleteDC(memDc);
        ReleaseDC(inst.hostHwnd, windowDc);
        return false;
    }

    HGDIOBJ oldBmp = SelectObject(memDc, dib);
    const BOOL printed = PrintWindow(inst.hostHwnd, memDc, PW_RENDERFULLCONTENT);
    SelectObject(memDc, oldBmp);

    if (printed)
    {
        inst.cpuFrameBgra.resize(static_cast<size_t>(w) * static_cast<size_t>(h) * 4u);
        memcpy(inst.cpuFrameBgra.data(), bits, inst.cpuFrameBgra.size());
        inst.cpuFrameWidth = w;
        inst.cpuFrameHeight = h;
        inst.cpuFrameValid = true;
    }

    DeleteObject(dib);
    DeleteDC(memDc);
    ReleaseDC(inst.hostHwnd, windowDc);
    return printed == TRUE;
}

static void UNITY_INTERFACE_API OnGraphicsDeviceEvent(UnityGfxDeviceEventType eventType)
{
    switch (eventType)
    {
    case kUnityGfxDeviceEventInitialize:
        EnsureUnityInterfaces();
        if (g_GraphicsD3D11)
            g_UnityDevice = g_GraphicsD3D11->GetDevice();
        break;
    case kUnityGfxDeviceEventShutdown:
        g_UnityDevice = nullptr;
        break;
    default:
        break;
    }
}

extern "C" {

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
    g_UnityPluginLoadCalled = true;
    g_UnityInterfaces = unityInterfaces;
    EnsureUnityInterfaces();
    if (g_Graphics && g_DeviceCallbackRegistered)
        OnGraphicsDeviceEvent(kUnityGfxDeviceEventInitialize);
    TryAcquireUnityDevice();
    EnsureWinRt();
}

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload()
{
    if (g_Graphics && g_DeviceCallbackRegistered)
        g_Graphics->UnregisterDeviceEventCallback(OnGraphicsDeviceEvent);
    std::lock_guard<std::mutex> lock(g_Mutex);
    g_Instances.clear();
    g_Environment.Reset();
    g_EnvironmentReady = false;
    g_UnityDevice = nullptr;
    g_Graphics = nullptr;
    g_GraphicsD3D11 = nullptr;
    g_UnityInterfaces = nullptr;
    g_UnityPluginLoadCalled = false;
    g_DeviceCallbackRegistered = false;
}

WEBVIEWGPU_API int WebViewGpu_IsSupported()
{
    return g_Graphics && g_Graphics->GetRenderer() == kUnityGfxRendererD3D11;
}

WEBVIEWGPU_API int WebViewGpu_GetApiVersion()
{
    return 14;
}

WEBVIEWGPU_API void WebViewGpu_GetPluginDiagnostics(int* outPluginLoaded, int* outRenderer, int* outHasD3D11, int* outDeviceReady)
{
    EnsureUnityInterfaces();
    TryAcquireUnityDevice();
    if (outPluginLoaded)
        *outPluginLoaded = g_UnityPluginLoadCalled ? 1 : 0;
    if (outRenderer)
        *outRenderer = g_Graphics ? static_cast<int>(g_Graphics->GetRenderer()) : -1;
    if (outHasD3D11)
        *outHasD3D11 = g_GraphicsD3D11 ? 1 : 0;
    if (outDeviceReady)
        *outDeviceReady = g_UnityDevice ? 1 : 0;
}

WEBVIEWGPU_API WebViewGpu_RenderEventFn WebViewGpu_GetRenderEventFunc()
{
    return OnRenderEvent;
}

WEBVIEWGPU_API void WebViewGpu_FlushGpuCaptures()
{
    TryAcquireUnityDevice();
    ProcessPendingGpuCaptures();
}

WEBVIEWGPU_API void WebViewGpu_GetCaptureDiagnostics(int handle, int* outHasSession, int* outHasFramePool, int* outHasOutput, int* outLastGpuCopyStage)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
    {
        if (outHasSession) *outHasSession = 0;
        if (outHasFramePool) *outHasFramePool = 0;
        if (outHasOutput) *outHasOutput = 0;
        if (outLastGpuCopyStage) *outLastGpuCopyStage = -1;
        return;
    }

    auto& inst = *it->second;
    if (outHasSession)
        *outHasSession = inst.captureSession ? 1 : 0;
    if (outHasFramePool)
        *outHasFramePool = inst.framePool ? 1 : 0;
    if (outHasOutput)
        *outHasOutput = (inst.outputTexture && inst.outputSrv) ? 1 : 0;
    if (outLastGpuCopyStage)
        *outLastGpuCopyStage = inst.lastGpuCopyStage;
}

WEBVIEWGPU_API int WebViewGpu_Create(int width, int height, int transparent)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    EnsureWinRt();

    auto inst = std::make_unique<WebViewGpuInstance>();
    inst->handle = g_NextHandle++;
    inst->width = std::max(1, width);
    inst->height = std::max(1, height);
    inst->transparent = transparent != 0;
    inst->hostHwnd = CreateHostWindow(inst->width, inst->height);

    const int handle = inst->handle;
    g_Instances.emplace(handle, std::move(inst));
    BeginCreateWebView(*g_Instances[handle]);
    RecreateOutputTexture(*g_Instances[handle]);
    return handle;
}

WEBVIEWGPU_API void WebViewGpu_Destroy(int handle)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
        return;

    auto& inst = *it->second;
    if (inst.webview && inst.messageToken.value != 0)
        inst.webview->remove_WebMessageReceived(inst.messageToken);

    if (inst.captureSession)
        inst.captureSession.Close();
    if (inst.framePool)
        inst.framePool.Close();

    if (inst.hostHwnd)
        DestroyWindow(inst.hostHwnd);

    g_Instances.erase(it);
}

WEBVIEWGPU_API int WebViewGpu_IsInitialized(int handle)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    return it != g_Instances.end() && it->second->initialized ? 1 : 0;
}

WEBVIEWGPU_API void WebViewGpu_LoadUrl(int handle, const char* urlUtf8)
{
    if (!urlUtf8)
        return;

    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
        return;

    auto& inst = *it->second;
    if (!inst.webview)
    {
        inst.pendingUrlUtf8 = urlUtf8;
        return;
    }

    NavigateInstanceUtf8(inst, urlUtf8);
}

WEBVIEWGPU_API void WebViewGpu_Resize(int handle, int width, int height)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
        return;

    auto& inst = *it->second;
    inst.width = std::max(1, width);
    inst.height = std::max(1, height);
    if (inst.hostHwnd)
        SetWindowPos(inst.hostHwnd, nullptr, 0, 0, inst.width, inst.height, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
    if (inst.initialized)
        ResizeComposition(inst);
}

WEBVIEWGPU_API void WebViewGpu_SetRenderScale(int handle, float scale)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
        return;

    auto& inst = *it->second;
    inst.renderScale = std::clamp(scale, 0.25f, 1.f);
    if (inst.initialized)
        ResizeComposition(inst);
}

WEBVIEWGPU_API void WebViewGpu_Tick(int handle, int captureFrame)
{
    WebViewGpuInstance* instPtr = nullptr;
    {
        std::lock_guard<std::mutex> lock(g_Mutex);
        auto it = g_Instances.find(handle);
        if (it == g_Instances.end())
            return;
        instPtr = it->second.get();
    }

    PumpHostMessages();
    TryAcquireUnityDevice();
    EnsureGpuResources(*instPtr);
    if (captureFrame)
    {
        if (GpuCaptureReady(*instPtr))
            instPtr->pendingGpuCapture = true;

        if (!GpuCaptureReady(*instPtr) || !instPtr->gpuFrameValid)
            CaptureHostWindowCpu(*instPtr);
    }
}

WEBVIEWGPU_API int WebViewGpu_HasGpuFrame(int handle)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    return it != g_Instances.end() && it->second->gpuFrameValid ? 1 : 0;
}

WEBVIEWGPU_API void* WebViewGpu_GetTexturePointer(int handle)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end() || !it->second->outputSrv || !it->second->gpuFrameValid)
        return nullptr;
    return it->second->outputSrv.Get();
}

WEBVIEWGPU_API int WebViewGpu_GetTextureWidth(int handle)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    return it != g_Instances.end() ? it->second->textureWidth : 0;
}

WEBVIEWGPU_API int WebViewGpu_GetTextureHeight(int handle)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    return it != g_Instances.end() ? it->second->textureHeight : 0;
}

WEBVIEWGPU_API int WebViewGpu_GetTextureFormatBgra()
{
    return (int)DXGI_FORMAT_B8G8R8A8_UNORM;
}

WEBVIEWGPU_API int WebViewGpu_HasCpuFrame(int handle)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    return it != g_Instances.end() && it->second->cpuFrameValid ? 1 : 0;
}

WEBVIEWGPU_API int WebViewGpu_CopyCpuFrame(int handle, unsigned char* dstBgra, int dstBytes, int* outWidth, int* outHeight)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end() || !it->second->cpuFrameValid)
        return 0;

    auto& inst = *it->second;
    if (outWidth)
        *outWidth = inst.cpuFrameWidth;
    if (outHeight)
        *outHeight = inst.cpuFrameHeight;

    const int required = inst.cpuFrameWidth * inst.cpuFrameHeight * 4;
    if (!dstBgra || dstBytes < required)
        return required;

    memcpy(dstBgra, inst.cpuFrameBgra.data(), static_cast<size_t>(required));
    return required;
}

WEBVIEWGPU_API void WebViewGpu_GetRuntimeStatus(int handle, int* outInitialized, int* outHasGpuTexture, int* outHasCpuFrame, int* outDeviceReady)
{
    TryAcquireUnityDevice();
    if (outDeviceReady)
        *outDeviceReady = g_UnityDevice ? 1 : 0;

    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
    {
        if (outInitialized) *outInitialized = 0;
        if (outHasGpuTexture) *outHasGpuTexture = 0;
        if (outHasCpuFrame) *outHasCpuFrame = 0;
        return;
    }

    auto& inst = *it->second;
    if (outInitialized)
        *outInitialized = inst.initialized ? 1 : 0;
    if (outHasGpuTexture)
        *outHasGpuTexture = inst.gpuFrameValid ? 1 : 0;
    if (outHasCpuFrame)
        *outHasCpuFrame = inst.cpuFrameValid ? 1 : 0;
}

static void MapPointer(WebViewGpuInstance& inst, int& x, int& y)
{
    const int viewW = std::max(1, static_cast<int>(inst.width * inst.renderScale));
    const int viewH = std::max(1, static_cast<int>(inst.height * inst.renderScale));
    x = std::clamp(static_cast<int>(x * inst.renderScale), 0, viewW - 1);
    y = std::clamp(static_cast<int>(y * inst.renderScale), 0, viewH - 1);
}

static bool DispatchCdpMouseEvent(WebViewGpuInstance& inst, const char* type, int x, int y, const char* button, int buttons, int clickCount)
{
    if (!inst.webview)
        return false;

    char paramsUtf8[320];
    sprintf_s(paramsUtf8,
        R"({"type":"%s","x":%d,"y":%d,"modifiers":0,"button":"%s","buttons":%d,"clickCount":%d})",
        type, x, y, button, buttons, clickCount);

    const int wideLen = MultiByteToWideChar(CP_UTF8, 0, paramsUtf8, -1, nullptr, 0);
    if (wideLen <= 1)
        return false;

    std::wstring paramsWide(static_cast<size_t>(wideLen - 1), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, paramsUtf8, -1, paramsWide.data(), wideLen);

    return SUCCEEDED(inst.webview->CallDevToolsProtocolMethod(
        L"Input.dispatchMouseEvent",
        paramsWide.c_str(),
        Microsoft::WRL::Callback<ICoreWebView2CallDevToolsProtocolMethodCompletedHandler>(
            [](HRESULT, LPCWSTR) -> HRESULT { return S_OK; }).Get()));
}

WEBVIEWGPU_API void WebViewGpu_PointerDown(int handle, int x, int y)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
        return;

    auto& inst = *it->second;
    MapPointer(inst, x, y);
    DispatchCdpMouseEvent(inst, "mouseMoved", x, y, "none", 0, 0);
    DispatchCdpMouseEvent(inst, "mousePressed", x, y, "left", 1, 1);
}

WEBVIEWGPU_API void WebViewGpu_PointerUp(int handle, int x, int y)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
        return;

    auto& inst = *it->second;
    MapPointer(inst, x, y);
    DispatchCdpMouseEvent(inst, "mouseReleased", x, y, "left", 0, 1);
}

WEBVIEWGPU_API void WebViewGpu_Click(int handle, int x, int y)
{
    char script[256]{};
    bool useScriptFallback = false;

    {
        std::lock_guard<std::mutex> lock(g_Mutex);
        auto it = g_Instances.find(handle);
        if (it == g_Instances.end())
            return;

        auto& inst = *it->second;
        MapPointer(inst, x, y);

        if (DispatchCdpMouseEvent(inst, "mouseMoved", x, y, "none", 0, 0) &&
            DispatchCdpMouseEvent(inst, "mousePressed", x, y, "left", 1, 1) &&
            DispatchCdpMouseEvent(inst, "mouseReleased", x, y, "left", 0, 1))
            return;

        sprintf_s(script,
            "(function(){var e=document.elementFromPoint(%d,%d);if(e){e.dispatchEvent(new MouseEvent('click',{bubbles:true,clientX:%d,clientY:%d}));}})();",
            x, y, x, y);
        useScriptFallback = true;
    }

    if (useScriptFallback)
        WebViewGpu_ExecuteJavaScript(handle, script);
}

WEBVIEWGPU_API void WebViewGpu_PointerMove(int handle, int x, int y)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
        return;

    auto& inst = *it->second;
    MapPointer(inst, x, y);
    DispatchCdpMouseEvent(inst, "mouseMoved", x, y, "none", 0, 0);
}

WEBVIEWGPU_API void WebViewGpu_PointerLeave(int handle)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
        return;

    DispatchCdpMouseEvent(*it->second, "mouseMoved", -1, -1, "none", 0, 0);
}

WEBVIEWGPU_API void WebViewGpu_Scroll(int handle, int x, int y, float deltaY)
{
    char script[256];
    sprintf_s(script, "window.scrollBy(0,%f);", deltaY);
    WebViewGpu_ExecuteJavaScript(handle, script);
    (void)x;
    (void)y;
}

WEBVIEWGPU_API void WebViewGpu_SendKey(int handle, int virtualKey, int keyDown)
{
    const char* keyDownName = keyDown ? "keydown" : "keyup";
    char script[256];
    sprintf_s(script, "document.dispatchEvent(new KeyboardEvent('%s',{keyCode:%d,bubbles:true}));", keyDownName, virtualKey);
    WebViewGpu_ExecuteJavaScript(handle, script);
    (void)virtualKey;
}

WEBVIEWGPU_API void WebViewGpu_ExecuteJavaScript(int handle, const char* scriptUtf8)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end() || !it->second->webview || !scriptUtf8)
        return;

    const int len = MultiByteToWideChar(CP_UTF8, 0, scriptUtf8, -1, nullptr, 0);
    std::wstring script(len - 1, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, scriptUtf8, -1, script.data(), len);
    it->second->webview->ExecuteScript(script.c_str(), nullptr);
}

WEBVIEWGPU_API void WebViewGpu_SetMessageCallback(int handle, WebViewGpu_MessageCallback callback, void* userData)
{
    std::lock_guard<std::mutex> lock(g_Mutex);
    auto it = g_Instances.find(handle);
    if (it == g_Instances.end())
        return;
    it->second->messageCallback = callback;
    it->second->messageUserData = userData;
}

WEBVIEWGPU_API void WebViewGpu_PostMessageToPage(int handle, const char* jsonUtf8)
{
    if (!jsonUtf8)
        return;
    std::string js = "window.dispatchEvent(new MessageEvent('message',{data:";
    js += jsonUtf8;
    js += "}));";
    WebViewGpu_ExecuteJavaScript(handle, js.c_str());
}

} // extern "C"

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
        g_Module = module;
    return TRUE;
}

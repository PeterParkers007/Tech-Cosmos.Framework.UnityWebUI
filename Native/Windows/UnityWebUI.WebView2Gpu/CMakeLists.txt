cmake_minimum_required(VERSION 3.20)
project(UnityWebUI.WebView2Gpu LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(NOT DEFINED WEBVIEW2_SDK_DIR)
    set(WEBVIEW2_SDK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../packages/Microsoft.Web.WebView2/build/native")
endif()

if(NOT EXISTS "${WEBVIEW2_SDK_DIR}/include/WebView2.h")
    message(FATAL_ERROR "WebView2 SDK not found. Run ../build.bat to fetch NuGet packages.")
endif()

add_library(UnityWebUI.WebView2Gpu SHARED
    src/WebViewGpuPlugin.cpp
)

target_compile_definitions(UnityWebUI.WebView2Gpu PRIVATE UNITYWEBUI_WEBVIEW2GPU_EXPORTS WIN32_LEAN_AND_MEAN NOMINMAX)

target_include_directories(UnityWebUI.WebView2Gpu PRIVATE
    include
    ../UnityPlugin
    "${WEBVIEW2_SDK_DIR}/include"
)

target_link_directories(UnityWebUI.WebView2Gpu PRIVATE
    "${WEBVIEW2_SDK_DIR}/x64"
)

target_link_libraries(UnityWebUI.WebView2Gpu PRIVATE
    WebView2LoaderStatic.lib
    d3d11.lib
    dxgi.lib
    shlwapi.lib
    windowsapp.lib
)

set_target_properties(UnityWebUI.WebView2Gpu PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/../../../Plugins/Windows/x86_64"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_CURRENT_SOURCE_DIR}/../../../Plugins/Windows/x86_64"
)

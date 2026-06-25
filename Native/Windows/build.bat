@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

rem NuGet packages must live OUTSIDE Assets so Unity does not import WPF/WinRT DLLs.
cd /d "%~dp0..\..\..\.."
set "PROJECT_ROOT=%CD%"
set "PACKAGES_DIR=%PROJECT_ROOT%\WebView2Build\packages"
set "WEBVIEW2_SDK=%PACKAGES_DIR%\Microsoft.Web.WebView2\build\native"
cd /d "%~dp0"

set NUGET=%~dp0nuget.exe
if not exist "%NUGET%" (
    where nuget >nul 2>&1
    if errorlevel 1 (
        echo [UnityWebUI] Downloading nuget.exe...
        powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile '%NUGET%'"
        if not exist "%NUGET%" (
            echo [UnityWebUI] Failed to download nuget.exe
            exit /b 1
        )
    ) else (
        set NUGET=nuget
    )
)

if not exist "%PACKAGES_DIR%" mkdir "%PACKAGES_DIR%"
"%NUGET%" install Microsoft.Web.WebView2 -OutputDirectory "%PACKAGES_DIR%" -ExcludeVersion
if errorlevel 1 (
    echo [UnityWebUI] NuGet install failed.
    exit /b 1
)

if not exist "%WEBVIEW2_SDK%\include\WebView2.h" (
    echo [UnityWebUI] WebView2 SDK not found at %WEBVIEW2_SDK%
    exit /b 1
)

rem Remove legacy packages folder if it was created under Assets (causes Unity import errors).
if exist "%~dp0packages" (
    echo [UnityWebUI] Removing legacy Assets/.../packages folder...
    rmdir /s /q "%~dp0packages"
)
if exist "%~dp0packages.meta" del /f /q "%~dp0packages.meta"

if not exist "%ProgramFiles(x86)%\Windows Kits\10\Include" (
    echo.
    echo [UnityWebUI] ERROR: Windows 10/11 SDK is NOT installed.
    echo.
    echo Fix:
    echo   1. Open "Visual Studio Installer"
    echo   2. Modify VS 2022 Community
    echo   3. Workloads: check "Desktop development with C++"
    echo   4. Individual components: check "Windows 11 SDK" or "Windows 10 SDK"
    echo   5. Apply, then run this build again
    echo.
    exit /b 1
)

set MSBUILD=
set VCVARS=
for %%G in (Community Professional Enterprise BuildTools) do (
    if exist "%ProgramFiles%\Microsoft Visual Studio\2022\%%G\MSBuild\Current\Bin\amd64\MSBuild.exe" (
        set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\%%G\MSBuild\Current\Bin\amd64\MSBuild.exe"
        set "VCVARS=%ProgramFiles%\Microsoft Visual Studio\2022\%%G\VC\Auxiliary\Build\vcvars64.bat"
        goto :have_msbuild
    )
)
where msbuild >nul 2>&1
if not errorlevel 1 (
    set MSBUILD=msbuild
    goto :have_msbuild
)
echo [UnityWebUI] MSBuild not found. Install Visual Studio 2022 with "Desktop development with C++".
exit /b 1

:have_msbuild
echo [UnityWebUI] Using MSBuild: !MSBUILD!

if defined VCVARS if exist "!VCVARS!" (
    echo [UnityWebUI] Initializing VS x64 toolchain...
    call "!VCVARS!"
)

"%MSBUILD%" "UnityWebUI.WebView2Gpu\UnityWebUI.WebView2Gpu.vcxproj" /p:Configuration=Release /p:Platform=x64 /p:WebView2SdkDir="%WEBVIEW2_SDK%" /p:NativeBuildRoot="%PROJECT_ROOT%\WebView2Build\Native" /m /v:minimal
if errorlevel 1 (
    echo.
    echo [UnityWebUI] MSBuild failed. If you see LNK1104, close WebView Preview or restart Unity.
    exit /b 1
)

set "BUILT_DLL=%PROJECT_ROOT%\WebView2Build\Native\x64\Release\_out\UnityWebUI.WebView2Gpu.dll"
set "PLUGIN_DIR=%PROJECT_ROOT%\Assets\UnityWebUI\Plugins\Windows\x86_64"
set "PLUGIN_DLL=%PLUGIN_DIR%\UnityWebUI.WebView2Gpu.dll"

if not exist "%BUILT_DLL%" (
    echo [UnityWebUI] Built DLL not found at %BUILT_DLL%
    exit /b 1
)

if not exist "%PLUGIN_DIR%" mkdir "%PLUGIN_DIR%"
copy /Y "%BUILT_DLL%" "%PLUGIN_DLL%" >nul
if errorlevel 1 (
    copy /Y "%BUILT_DLL%" "%PLUGIN_DIR%\UnityWebUI.WebView2Gpu.dll.pending" >nul
    echo [UnityWebUI] Compile succeeded. Plugins copy skipped ^(DLL locked^).
    echo [UnityWebUI] Unity will apply the update after script reload.
) else (
    echo [UnityWebUI] Built UnityWebUI.WebView2Gpu.dll -^> Assets/UnityWebUI/Plugins/Windows/x86_64/
)
endlocal
exit /b 0

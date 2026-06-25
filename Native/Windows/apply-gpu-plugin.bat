@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

if defined UNITYWEBUI_PACKAGE_ROOT (
    for %%I in ("%UNITYWEBUI_PACKAGE_ROOT%") do set "PACKAGE_ROOT=%%~fI"
) else (
    for %%I in ("%~dp0..\..") do set "PACKAGE_ROOT=%%~fI"
)

if defined UNITYWEBUI_PROJECT_ROOT (
    for %%I in ("%UNITYWEBUI_PROJECT_ROOT%") do set "PROJECT_ROOT=%%~fI"
) else (
    set "SEARCH=%PACKAGE_ROOT%"
    :find_project_root
    if exist "!SEARCH!\ProjectSettings\ProjectVersion.txt" (
        set "PROJECT_ROOT=!SEARCH!"
        goto :have_project_root
    )
    for %%I in ("!SEARCH!\..") do set "SEARCH=%%~fI"
    if /I not "!SEARCH!"=="!SEARCH!\.." goto :find_project_root
    echo [UnityWebUI] ERROR: Could not find Unity project root from %PACKAGE_ROOT%
    goto :fail
    :have_project_root
)

set "SRC=%PROJECT_ROOT%\WebView2Build\Native\x64\Release\_out\UnityWebUI.WebView2Gpu.dll"
set "PLUGIN_DIR=%PACKAGE_ROOT%\Plugins\Windows\x86_64"
set "DST=%PLUGIN_DIR%\UnityWebUI.WebView2Gpu.dll"
set "PENDING=%PLUGIN_DIR%\UnityWebUI.WebView2Gpu.dll.pending"
set "NO_PAUSE=0"
if /I "%~1"=="/nopause" set "NO_PAUSE=1"
if /I "%~1"=="--nopause" set "NO_PAUSE=1"

if not exist "%SRC%" (
    echo [UnityWebUI] Built DLL missing. Run build.bat first.
    echo Expected: %SRC%
    goto :fail
)

if not exist "%PLUGIN_DIR%" mkdir "%PLUGIN_DIR%"

copy /Y "%SRC%" "%DST%"
if errorlevel 1 (
    echo [UnityWebUI] Copy failed. Close Unity completely, then run this script again.
    copy /Y "%SRC%" "%PENDING%" >nul
    echo [UnityWebUI] Saved to .pending for next editor launch.
    goto :fail
)

if exist "%PENDING%" del /f /q "%PENDING%"
echo [UnityWebUI] GPU plugin updated: %DST%
echo [UnityWebUI] Restart Unity Editor if it was open.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:fail
if "%NO_PAUSE%"=="0" pause
exit /b 1

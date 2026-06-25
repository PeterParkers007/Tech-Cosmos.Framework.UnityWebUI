@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

if defined UNITYWEBUI_PACKAGE_ROOT (
    for %%I in ("%UNITYWEBUI_PACKAGE_ROOT%") do set "PACKAGE_ROOT=%%~fI"
) else (
    for %%I in ("%~dp0..\..") do set "PACKAGE_ROOT=%%~fI"
)

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
set "VERSION_FILE=%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"
set "APPLY_BAT=%~dp0apply-gpu-plugin.bat"
set "FIND_UNITY_BAT=%~dp0find-unity-editor.bat"
set "BUILT_DLL=%PROJECT_ROOT%\WebView2Build\Native\x64\Release\_out\UnityWebUI.WebView2Gpu.dll"
set "EXECUTE_METHOD=UnityWebUI.Editor.WebViewGpuPluginRestartHelper.EnterPlayModeAfterLoad"

echo ============================================================
echo  UnityWebUI - Close Unity ^> Apply GPU plugin ^> Reopen ^> Play
echo ============================================================
echo Project: %PROJECT_ROOT%
echo Package: %PACKAGE_ROOT%
echo.

if not exist "%VERSION_FILE%" (
    echo [UnityWebUI] ERROR: ProjectSettings\ProjectVersion.txt not found.
    echo Expected Unity project root: %PROJECT_ROOT%
    goto :fail
)

if not exist "%BUILT_DLL%" (
    echo [UnityWebUI] Built DLL missing. Run build.bat first.
    goto :fail
)

set "UNITY_VERSION="
for /f "usebackq tokens=1,* delims=:" %%A in ("%VERSION_FILE%") do (
    if /I "%%A"=="m_EditorVersion" set "UNITY_VERSION=%%B"
)
set "UNITY_VERSION=%UNITY_VERSION: =%"
if "%UNITY_VERSION%"=="" (
    echo [UnityWebUI] ERROR: Could not read m_EditorVersion from ProjectVersion.txt
    goto :fail
)

echo [UnityWebUI] Unity version: %UNITY_VERSION%

tasklist /FI "IMAGENAME eq Unity.exe" 2>nul | find /I "Unity.exe" >nul
if not errorlevel 1 (
    echo [UnityWebUI] Closing Unity...
    taskkill /IM Unity.exe /F >nul 2>&1
    timeout /t 2 /nobreak >nul
)

tasklist /FI "IMAGENAME eq Unity.exe" 2>nul | find /I "Unity.exe" >nul
if not errorlevel 1 (
    echo [UnityWebUI] ERROR: Unity.exe is still running. Close it manually and run again.
    goto :fail
)

echo [UnityWebUI] Unity closed.
set "UNITYWEBUI_PACKAGE_ROOT=%PACKAGE_ROOT%"
set "UNITYWEBUI_PROJECT_ROOT=%PROJECT_ROOT%"
call "%APPLY_BAT%" /nopause
if errorlevel 1 goto :fail

set "UNITY_EXE="
if exist "%FIND_UNITY_BAT%" (
    for /f "usebackq delims=" %%I in (`call "%FIND_UNITY_BAT%" "%UNITY_VERSION%"`) do set "UNITY_EXE=%%I"
)

if not defined UNITY_EXE (
    echo [UnityWebUI] ERROR: Unity.exe not found for version %UNITY_VERSION%.
    goto :fail
)

echo [UnityWebUI] Using: !UNITY_EXE!
start "" "!UNITY_EXE!" -projectPath "%PROJECT_ROOT%" -executeMethod %EXECUTE_METHOD%
echo [UnityWebUI] Done. Unity is starting; Play Mode will auto-start after scripts compile.
exit /b 0

:fail
pause
exit /b 1

@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

set "PROJECT_ROOT=%~dp0..\..\..\.."
for %%I in ("%PROJECT_ROOT%") do set "PROJECT_ROOT=%%~fI"
set "VERSION_FILE=%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"
set "APPLY_BAT=%~dp0apply-gpu-plugin.bat"
set "FIND_UNITY_BAT=%~dp0find-unity-editor.bat"
set "BUILT_DLL=%PROJECT_ROOT%\WebView2Build\Native\x64\Release\_out\UnityWebUI.WebView2Gpu.dll"
set "EXECUTE_METHOD=UnityWebUI.Editor.WebViewGpuPluginRestartHelper.EnterPlayModeAfterLoad"

echo ============================================================
echo  UnityWebUI - Close Unity ^> Apply GPU plugin ^> Reopen ^> Play
echo ============================================================
echo Project: %PROJECT_ROOT%
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
for /f "usebackq tokens=1,* delims=:" %%A in (`findstr /B /I "m_EditorVersion:" "%VERSION_FILE%"`) do (
    set "UNITY_VERSION=%%B"
)
set "UNITY_VERSION=%UNITY_VERSION: =%"
if not defined UNITY_VERSION (
    echo [UnityWebUI] ERROR: Could not read m_EditorVersion from ProjectVersion.txt
    goto :fail
)

rem Major prefix for fuzzy folder match (2022.3.48f1c1 -^> 2022.3.48)
set "UNITY_MAJOR=%UNITY_VERSION%"
for /f "tokens=1,2,3 delims=." %%A in ("%UNITY_VERSION%") do set "UNITY_MAJOR=%%A.%%B.%%C"
echo [UnityWebUI] Unity version: %UNITY_VERSION%
echo.

echo [1/4] Closing Unity Editor (all Unity.exe instances)...
echo        Press Ctrl+C within 3 seconds to cancel.
timeout /t 3 /nobreak >nul

tasklist /FI "IMAGENAME eq Unity.exe" 2>nul | find /I "Unity.exe" >nul
if not errorlevel 1 (
    taskkill /IM Unity.exe /T /F >nul 2>&1
    set /a WAIT_LEFT=30
    :wait_unity_exit
    tasklist /FI "IMAGENAME eq Unity.exe" 2>nul | find /I "Unity.exe" >nul
    if errorlevel 1 goto :unity_closed
    timeout /t 1 /nobreak >nul
    set /a WAIT_LEFT-=1
    if !WAIT_LEFT! GTR 0 goto :wait_unity_exit
    echo [UnityWebUI] ERROR: Unity.exe is still running. Close it manually and run again.
    goto :fail
)
:unity_closed
echo [UnityWebUI] Unity closed.
echo.

echo [2/4] Applying GPU plugin...
call "%APPLY_BAT%" /nopause
if errorlevel 1 goto :fail
echo.

echo [3/4] Locating Unity.exe...
set "UNITY_EXE="
call "%FIND_UNITY_BAT%"
if errorlevel 1 (
    echo [UnityWebUI] ERROR: Unity.exe not found for version %UNITY_VERSION%.
    echo.
    echo Tried:
    echo   - Unity Hub secondary path from %%APPDATA%%\UnityHub\secondaryInstallPath.json
    echo   - %%ProgramFiles%%\Unity\Hub\Editor\%UNITY_VERSION%\Editor\Unity.exe
    echo   - PATH
    echo.
    echo Fix: copy unity-editor-path.local.example.bat to unity-editor-path.local.bat
    echo       and set UNITY_EXE to your Unity.exe path.
    goto :fail
)
echo [UnityWebUI] Using: !UNITY_EXE!
echo.

echo [4/4] Launching Unity and entering Play Mode when ready...
start "" "!UNITY_EXE!" -projectPath "%PROJECT_ROOT%" -executeMethod %EXECUTE_METHOD%
if errorlevel 1 (
    echo [UnityWebUI] ERROR: Failed to start Unity.
    goto :fail
)
echo [UnityWebUI] Done. Unity is starting; Play Mode will auto-start after scripts compile.
echo.
pause
exit /b 0

:fail
echo.
pause
exit /b 1

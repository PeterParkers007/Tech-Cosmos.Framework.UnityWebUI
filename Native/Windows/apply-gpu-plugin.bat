@echo off
setlocal
cd /d "%~dp0..\..\..\.."
set "SRC=%CD%\WebView2Build\Native\x64\Release\_out\UnityWebUI.WebView2Gpu.dll"
set "DST=%CD%\Assets\UnityWebUI\Plugins\Windows\x86_64\UnityWebUI.WebView2Gpu.dll"
set "PENDING=%CD%\Assets\UnityWebUI\Plugins\Windows\x86_64\UnityWebUI.WebView2Gpu.dll.pending"
set "NO_PAUSE=0"
if /I "%~1"=="/nopause" set "NO_PAUSE=1"
if /I "%~1"=="--nopause" set "NO_PAUSE=1"

if not exist "%SRC%" (
    echo [UnityWebUI] Built DLL missing. Run build.bat first.
    echo.
    if "%NO_PAUSE%"=="0" pause
    exit /b 1
)

if not exist "%~dp0..\..\Plugins\Windows\x86_64" mkdir "%~dp0..\..\Plugins\Windows\x86_64"

copy /Y "%SRC%" "%DST%"
if errorlevel 1 (
    echo [UnityWebUI] Copy failed. Close Unity completely, then run this script again.
    copy /Y "%SRC%" "%PENDING%" >nul
    echo [UnityWebUI] Saved to .pending for next editor launch.
    echo.
    if "%NO_PAUSE%"=="0" pause
    exit /b 1
)

if exist "%PENDING%" del /f /q "%PENDING%"
echo [UnityWebUI] GPU plugin updated: %DST%
echo [UnityWebUI] Restart Unity Editor if it was open.
echo.
if "%NO_PAUSE%"=="0" pause
exit /b 0

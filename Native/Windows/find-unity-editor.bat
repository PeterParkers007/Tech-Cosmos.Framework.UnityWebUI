@echo off
set "UNITY_EXE="

if exist "%~dp0unity-editor-path.local.bat" call "%~dp0unity-editor-path.local.bat"
if defined UNITY_EXE if exist "%UNITY_EXE%" exit /b 0

if not defined UNITY_VERSION exit /b 1

call :TryUnityExe "%ProgramFiles%\Unity\Hub\Editor\%UNITY_VERSION%\Editor\Unity.exe"
call :TryUnityExe "%ProgramFiles(x86)%\Unity\Hub\Editor\%UNITY_VERSION%\Editor\Unity.exe"

set "HUB_ROOT="
for /f "usebackq delims=" %%I in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0find-unity-hub-path.ps1" 2^>nul`) do set "HUB_ROOT=%%I"
if defined HUB_ROOT (
    call :TryUnityExe "%HUB_ROOT%\%UNITY_VERSION%\Editor\Unity.exe"
    call :TryUnityExe "%HUB_ROOT%\Hub\Editor\%UNITY_VERSION%\Editor\Unity.exe"
    for /d %%D in ("%HUB_ROOT%\*") do call :TryUnityExe "%%~D\Editor\Unity.exe"
)

if defined UNITY_MAJOR (
    for %%R in ("%ProgramFiles%\Unity\Hub\Editor" "%ProgramFiles(x86)%\Unity\Hub\Editor") do (
        if exist %%R (
            for /f "delims=" %%D in ('dir /b /ad "%%~fR" 2^>nul ^| findstr /I /B "%UNITY_MAJOR%"') do (
                call :TryUnityExe "%%~fR\%%D\Editor\Unity.exe"
            )
        )
    )
)

for /f "delims=" %%U in ('where Unity.exe 2^>nul') do call :TryUnityExe "%%U"
if defined UNITY_EXE exit /b 0
exit /b 1

:TryUnityExe
if defined UNITY_EXE exit /b 0
if exist "%~1" set "UNITY_EXE=%~1"
exit /b 0

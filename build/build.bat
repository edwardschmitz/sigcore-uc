@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=C:\Users\eddie\OneDrive\Documents\GitHub\sigcore-uc"
set "LOG=%TEMP%\sigcore-build.log"

cd /d "%ROOT%" || exit /b 1

echo Publishing...
> "%LOG%" echo.

rem Existing Raspberry Pi build sequence. Do not change these publish settings.
call :publish "Server" "software\Server\SigCoreServer\SigCoreServer.csproj" || goto :fail
call :publish "WebBridge" "software\WebBridge\WebBridge.csproj" || goto :fail
call :publish "Updater" "software\SigCoreUpdater\SigCoreUpdater.csproj" || goto :fail
call :publish "Launcher" "software\SigCoreLauncher\SigCoreLauncher.csproj" || goto :fail

rem Locate the projects and installer scripts without assuming their source folder names.
call :find_file "SigCoreCommon.csproj" COMMON_PROJECT || goto :fail
call :find_file "ControlPanel.csproj" CONTROL_PANEL_PROJECT || goto :fail
call :find_file "SigCoreCommon.iss" COMMON_ISS || goto :fail
call :find_file "ControlPanelInstaller.iss" CONTROL_PANEL_ISS || goto :fail

rem Both Windows projects must have the same version. Control Panel is authoritative.
set "CP_VERSION="
set "COMMON_VERSION="
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "$x=[xml](Get-Content -LiteralPath '%CONTROL_PANEL_PROJECT%' -Raw); $x.Project.PropertyGroup.Version | Select-Object -First 1"`) do set "CP_VERSION=%%V"
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "$x=[xml](Get-Content -LiteralPath '%COMMON_PROJECT%' -Raw); $x.Project.PropertyGroup.Version | Select-Object -First 1"`) do set "COMMON_VERSION=%%V"
if not defined CP_VERSION (
    echo ERROR: Control Panel version missing.
    goto :fail
)
if not "%CP_VERSION%"=="%COMMON_VERSION%" (
    echo ERROR: Windows project versions are different: %CP_VERSION% and %COMMON_VERSION%.
    goto :fail
)
echo   Windows version: %CP_VERSION%

rem The Common installer packages the publish directory, not the normal build output.
echo   SigCoreCommon...
dotnet publish "%COMMON_PROJECT%" -c Release -v:q >> "%LOG%" 2>&1 || goto :fail
if not exist "%ROOT%\build\sigcorecommon\Release\net8.0\publish\SigCoreCommon.dll" (
    echo ERROR: SigCoreCommon publish output is missing.
    goto :fail
)
echo   SigCoreCommon done.

echo   Control Panel...
dotnet build "%CONTROL_PANEL_PROJECT%" -c Release -v:q >> "%LOG%" 2>&1 || goto :fail
if not exist "%ROOT%\build\control-panel\Release\net8.0-windows\ControlPanel.exe" (
    echo ERROR: Control Panel build output is missing.
    goto :fail
)
echo   Control Panel done.

rem Find the installed Inno Setup compiler, without assuming it is on PATH.
set "ISCC="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC (
    where ISCC.exe >nul 2>&1
    if not errorlevel 1 set "ISCC=ISCC.exe"
)
if not defined ISCC (
    echo ERROR: Inno Setup 6 compiler ISCC.exe was not found.
    goto :fail
)

echo   Control Panel installer...
"%ISCC%" "/DWindowsVersion=%CP_VERSION%" "%CONTROL_PANEL_ISS%" >> "%LOG%" 2>&1 || goto :fail
if not exist "%ROOT%\build\installers\ControlPanelSetup.exe" (
    echo ERROR: Control Panel installer output is missing.
    goto :fail
)
echo   Control Panel installer done.

echo   SigCoreCommon installer...
"%ISCC%" "/DWindowsVersion=%CP_VERSION%" "%COMMON_ISS%" >> "%LOG%" 2>&1 || goto :fail
if not exist "%ROOT%\build\installers\SigCoreCommon.exe" (
    echo ERROR: SigCoreCommon installer output is missing.
    goto :fail
)
echo   SigCoreCommon installer done.

echo Build complete.
echo Installers: %ROOT%\build\installers
exit /b 0

:publish
set "NAME=%~1"
set "PROJECT=%~2"
echo   %NAME%...
set "START=%TIME%"
dotnet publish "%PROJECT%" -c Release -r linux-arm64 -v:q >> "%LOG%" 2>&1
if errorlevel 1 (
    echo   %NAME% FAILED.
    exit /b 1
)
set "END=%TIME%"
echo   %NAME% done.  [%START% - %END%]
exit /b 0

:find_file
set "FOUND="
for /r "%ROOT%\software" %%F in (%~1) do (
    if not defined FOUND set "FOUND=%%~fF"
)
if not defined FOUND (
    echo ERROR: Cannot locate %~1 under software.
    exit /b 1
)
set "%~2=%FOUND%"
exit /b 0

:fail
echo ERROR: Build failed.
echo Build details: %LOG%
exit /b 1

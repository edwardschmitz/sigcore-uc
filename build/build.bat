@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=C:\Users\eddie\OneDrive\Documents\GitHub\sigcore-uc"
set "LOG=%TEMP%\sigcore-build.log"

cd /d "%ROOT%" || exit /b 1

echo Publishing...
> "%LOG%" echo.

call :publish "Server" "software\Server\SigCoreServer\SigCoreServer.csproj" || goto :fail
call :publish "WebBridge" "software\WebBridge\WebBridge.csproj" || goto :fail
call :publish "Updater" "software\SigCoreUpdater\SigCoreUpdater.csproj" || goto :fail
call :publish "Launcher" "software\SigCoreLauncher\SigCoreLauncher.csproj" || goto :fail

echo Build complete.
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

:fail
echo ERROR: Publish failed.
echo Build details: %LOG%
exit /b 1

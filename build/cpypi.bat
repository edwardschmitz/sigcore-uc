@echo off
setlocal EnableExtensions EnableDelayedExpansion

if "%~1"=="" (
    echo ERROR: IP address is required.
    echo Usage: cpypi IP_ADDRESS
    exit /b 1
)

set "ROOT=C:\Users\eddie\OneDrive\Documents\GitHub\sigcore-uc"
set "PI=sigcoreuc@%~1"

cd /d "%ROOT%" || exit /b 1

for /f "delims=" %%V in ('ssh %PI% "readlink -f /opt/sigcore/current"') do set "REMOTE_VERSION_DIR=%%V"

if not defined REMOTE_VERSION_DIR (
    echo ERROR: Could not determine active SigCore version on Pi.
    exit /b 1
)

echo Deploying to %REMOTE_VERSION_DIR%...

ssh %PI% "sudo systemctl stop sigcorelauncher.service" >nul || goto :deployfail

scp -q "build\server\publish\sigcoreserver" %PI%:/tmp/sigcoreserver || goto :deployfail
scp -q "build\webbridge\publish\WebBridge" %PI%:/tmp/WebBridge || goto :deployfail
scp -q "build\updater\publish\SigCoreUpdater" %PI%:/tmp/SigCoreUpdater || goto :deployfail
scp -q "build\launcher\publish\SigCoreLauncher" %PI%:/tmp/SigCoreLauncher || goto :deployfail

ssh %PI% "sudo install -m 755 /tmp/sigcoreserver %REMOTE_VERSION_DIR%/sigcoreserver && sudo install -m 755 /tmp/WebBridge %REMOTE_VERSION_DIR%/WebBridge && sudo install -m 755 /tmp/SigCoreUpdater %REMOTE_VERSION_DIR%/SigCoreUpdater && sudo install -m 755 /tmp/SigCoreLauncher /opt/sigcore/SigCoreLauncher && rm -f /tmp/sigcoreserver /tmp/WebBridge /tmp/SigCoreUpdater /tmp/SigCoreLauncher" >nul || goto :deployfail

ssh %PI% "sudo systemctl start sigcorelauncher.service" >nul || goto :deployfail

echo Verifying...
ssh %PI% "sleep 2; systemctl is-active sigcorelauncher.service"

echo Done.
exit /b 0

:deployfail
echo ERROR: Deploy failed.
exit /b 1

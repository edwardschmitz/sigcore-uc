@echo off
setlocal EnableExtensions EnableDelayedExpansion

if "%~1"=="" (
    echo ERROR: IP address is required.
    echo Usage: cpywww IP_ADDRESS
    exit /b 1
)

set "PI=sigcoreuc@%~1"
set "WWW=%~dp0..\software\UpdaterFiles\www"

if not exist "%WWW%\index.html" (
    echo ERROR: Website source not found: "%WWW%"
    echo Expected location: software\UpdaterFiles\www relative to the repository root.
    exit /b 1
)

set "REMOTE_VERSION_DIR="
for /f "delims=" %%V in ('ssh %PI% "readlink -f /opt/sigcore/current"') do set "REMOTE_VERSION_DIR=%%V"

if not defined REMOTE_VERSION_DIR (
    echo ERROR: Could not determine active SigCore version on Pi.
    exit /b 1
)

echo Deploying website to %REMOTE_VERSION_DIR%/www/...

ssh %PI% "sudo rm -rf /tmp/sigcore-www-upload && mkdir -p /tmp/sigcore-www-upload" || goto :deployfail
scp -q -r "%WWW%/." %PI%:/tmp/sigcore-www-upload/ || goto :deployfail
ssh %PI% "sudo mkdir -p '%REMOTE_VERSION_DIR%/www' && sudo cp -r /tmp/sigcore-www-upload/. '%REMOTE_VERSION_DIR%/www/' && sudo find '%REMOTE_VERSION_DIR%/www' -type d -exec chmod 755 {} + && sudo find '%REMOTE_VERSION_DIR%/www' -type f -exec chmod 644 {} + && sudo rm -rf /tmp/sigcore-www-upload" || goto :deployfail

echo Website deployment complete. No services restarted.
exit /b 0

:deployfail
echo ERROR: Website deployment failed.
exit /b 1

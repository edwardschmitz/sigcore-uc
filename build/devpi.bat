@echo off

if "%~1"=="" (
    echo ERROR: IP address is required.
    echo Usage: devpi IP_ADDRESS
    exit /b 1
)

call "%~dp0build.bat"
if errorlevel 1 goto :buildfail

call "%~dp0cpypi.bat" "%~1"
exit /b %errorlevel%

:buildfail
echo ERROR: Build failed.
exit /b 1

@echo off
setlocal EnableExtensions

if "%~1"=="" (
    echo ERROR: IP address is required.
    echo Usage: devpi IP_ADDRESS
    exit /b 1
)

call "%~dp0build.bat"
if errorlevel 1 goto :buildfail

rem Ensure the ModbusBridge executable was published before deploying.
rem This does not replace cpypi.bat's responsibility for copying release files.
if not exist "%~dp0modbusbridge\publish\ModbusBridge" (
    echo ERROR: ModbusBridge publish output was not found.
    echo Expected: "%~dp0modbusbridge\publish\ModbusBridge"
    exit /b 1
)

call "%~dp0cpypi.bat" "%~1"
if errorlevel 1 goto :deployfail
exit /b 0

:buildfail
echo ERROR: Build failed.
exit /b 1

:deployfail
echo ERROR: Deployment failed.
exit /b 1

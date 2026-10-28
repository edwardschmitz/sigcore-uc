@echo off
setlocal

set "TARGET_IP=%~1"
if not defined TARGET_IP (
    set /p "TARGET_IP=Development device IP [192.168.0.141]: "
    if not defined TARGET_IP set "TARGET_IP=192.168.0.141"
)

set "REGRESSION=%~dp0..\software\tools\regression.py"
if not exist "%REGRESSION%" (
    echo ERROR: Regression script not found: "%REGRESSION%"
    exit /b 1
)

echo Running SigCore regression with logging against %TARGET_IP%...
python "%REGRESSION%" --logging-ip "%TARGET_IP%"
exit /b %errorlevel%

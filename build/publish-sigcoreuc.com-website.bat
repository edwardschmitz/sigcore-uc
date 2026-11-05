@echo off
setlocal
rem Run the SigCore publisher stored under software\publisher from build\.
rem Forward all command-line arguments unchanged.
set "PUBLISHER=%~dp0..\software\publisher\publisher.py"
if not exist "%PUBLISHER%" (
    echo ERROR: Publisher not found: "%PUBLISHER%"
    exit /b 1
)
if "%~1"=="" (
    py -3 "%PUBLISHER%" publish
) else (
    py -3 "%PUBLISHER%" %*
)
exit /b %ERRORLEVEL%

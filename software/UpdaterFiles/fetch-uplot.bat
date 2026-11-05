@echo off
setlocal

rem Download the two locally hosted uPlot files for the SigCore device website.
rem Double-click this batch file from Windows Explorer.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0fetch-uplot.ps1"
set "result=%errorlevel%"

echo.
if not "%result%"=="0" (
    echo uPlot download FAILED. Verify internet access and try again.
    pause
    exit /b %result%
)

echo uPlot download complete.
echo The files are in UpdaterFiles\www\vendor\uplot\.
pause
exit /b 0

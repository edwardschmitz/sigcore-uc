@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem One Windows version shared by Control Panel and SigCoreCommon.
rem Return codes: 0=unchanged, 2=changed (continue build), 1=error.

set "CP_PROJECT=%~dp0..\software\ControlPanel\ControlPanel.csproj"
set "COMMON_PROJECT=%~dp0..\software\libs\SigCoreCommon\SigCoreCommon.csproj"

if not exist "%CP_PROJECT%" (
    echo ERROR: Cannot find "%CP_PROJECT%"
    exit /b 1
)
if not exist "%COMMON_PROJECT%" (
    echo ERROR: Cannot find "%COMMON_PROJECT%"
    exit /b 1
)

call :read_version "%CP_PROJECT%" CP_VERSION
if errorlevel 1 exit /b 1
call :read_version "%COMMON_PROJECT%" COMMON_VERSION
if errorlevel 1 exit /b 1

echo.
echo Windows components version:
echo   Control Panel: %CP_VERSION%
echo   SigCoreCommon: %COMMON_VERSION%
echo.

rem Existing project files may have no explicit version or different versions.
rem Let the user choose one shared version; never assume they match.
if "%CP_VERSION%"=="%COMMON_VERSION%" if not "%CP_VERSION%"=="NOT SET" goto :matched

echo The two projects do not have one explicit matching version.
echo A shared version must be selected before the release can continue.
echo No files will be changed unless you enter a valid version.
echo.
set "NEW_VERSION="
set /p "NEW_VERSION=Enter shared version (Major.Minor.Patch), or Enter to cancel: "
if not defined NEW_VERSION (
    echo No files changed.
    exit /b 1
)
call :write_versions
exit /b %ERRORLEVEL%

:matched
set "CURRENT=%CP_VERSION%"
choice /C YN /M "Increment patch version for Control Panel and Common"
if errorlevel 3 exit /b 1
if errorlevel 2 (
    echo Version unchanged: %CURRENT%
    exit /b 0
)
if not errorlevel 1 exit /b 1

set "NEW_VERSION="
for /f "delims=" %%V in ('powershell -NoProfile -NonInteractive -Command "$ErrorActionPreference='Stop'; $v=[version]$env:CURRENT; [Console]::WriteLine(('{0}.{1}.{2}' -f $v.Major,$v.Minor,($v.Build+1)))"') do set "NEW_VERSION=%%V"
if not defined NEW_VERSION (
    echo ERROR: Could not generate next patch version. No files changed.
    exit /b 1
)
echo Proposed new version: %NEW_VERSION%
call :write_versions
exit /b %ERRORLEVEL%

:read_version
set "VERSION_SOURCE=%~1"
set "READ_VERSION="
for /f "delims=" %%V in ('powershell -NoProfile -NonInteractive -Command "$ErrorActionPreference='Stop'; $x=[xml]([IO.File]::ReadAllText($env:VERSION_SOURCE)); $n=$x.SelectSingleNode('/Project/PropertyGroup/Version'); if($null -eq $n) { [Console]::WriteLine('NOT SET') } else { $v=$n.InnerText.Trim(); if($v -notmatch '^[0-9]+[.][0-9]+[.][0-9]+$') { throw ('Invalid Version: '+$v) }; [Console]::WriteLine($v) }"') do if not defined READ_VERSION set "READ_VERSION=%%V"
if not defined READ_VERSION (
    echo ERROR: Cannot read a valid version from "%VERSION_SOURCE%"
    exit /b 1
)
set "%~2=%READ_VERSION%"
exit /b 0

:write_versions
rem Validate new version and prepare BOTH edits before writing either project.
rem A failure during writing attempts to restore the original source contents.
powershell -NoProfile -NonInteractive -Command "$ErrorActionPreference='Stop'; $v=$env:NEW_VERSION; if($v -notmatch '^[0-9]+[.][0-9]+[.][0-9]+$') { throw 'Expected numeric Major.Minor.Patch version' }; $paths=@($env:CP_PROJECT,$env:COMMON_PROJECT); $old=@(); $new=@(); foreach($p in $paths){$s=[IO.File]::ReadAllText($p); $old+=,$s; $m=[regex]::Matches($s,'<Version>[^<]*</Version>'); if($m.Count -gt 1) { throw ('Multiple Version properties in '+$p) }; if($m.Count -eq 1) {$t=[regex]::Replace($s,'<Version>[^<]*</Version>','<Version>'+$v+'</Version>')} else {if($s -notmatch '<PropertyGroup>') {throw ('Missing PropertyGroup in '+$p)}; $t=[regex]::Replace($s,'<PropertyGroup>','<PropertyGroup>'+[Environment]::NewLine+'    <Version>'+$v+'</Version>',1)}; $new+=,$t}; try{for($i=0;$i -lt $paths.Count;$i++){[IO.File]::WriteAllText($paths[$i],$new[$i],(New-Object System.Text.UTF8Encoding($false)))}} catch{for($i=0;$i -lt $paths.Count;$i++){[IO.File]::WriteAllText($paths[$i],$old[$i],(New-Object System.Text.UTF8Encoding($false)))}; throw}"
if errorlevel 1 (
    echo ERROR: Could not update project versions.
    exit /b 1
)
call :read_version "%CP_PROJECT%" VERIFIED_CP
if errorlevel 1 exit /b 1
call :read_version "%COMMON_PROJECT%" VERIFIED_COMMON
if errorlevel 1 exit /b 1
if not "%VERIFIED_CP%"=="%NEW_VERSION%" (
    echo ERROR: Control Panel version verification failed.
    exit /b 1
)
if not "%VERIFIED_COMMON%"=="%NEW_VERSION%" (
    echo ERROR: SigCoreCommon version verification failed.
    exit /b 1
)
echo Both project versions updated to %NEW_VERSION%.
echo Continuing build-update with the updated version.
exit /b 2

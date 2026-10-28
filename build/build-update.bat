@echo off
setlocal EnableExtensions

rem -------- Keep deployment output readable --------
set "DOTNET_NOLOGO=1"
set "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true"

rem =============================================
rem  SigCore Rev B Update Build + Stage Script
rem
rem  Run from:
rem      sigcore-uc\build
rem
rem  Usage:
rem      build-update 2.0.0
rem
rem  Stages only to Hostinger PRE-LAUNCH.
rem  Promotion to the live revB folder is MANUAL.
rem =============================================

set "REV=revB"
set "VER=%~1"
if "%VER%"=="" (
    echo ERROR: Version number is required.
    echo Usage: build-update 2.0.0
    exit /b 1
)

rem -------- Paths --------
set "REPO=C:\Users\eddie\OneDrive\Documents\GitHub\sigcore-uc"
set "BUILDER_PROJECT=%REPO%\software\SigCoreUpdateBuilder\SigCoreUpdateBuilder.csproj"

set "BUILDER=%REPO%\build\update\Release\net8.0\SigCoreUpdateBuilder.exe"
set "SIGNING_KEY=%REPO%\keys\sigcore_private.pem"
set "DEPLOY_KEY=%REPO%\keys\sigcore_deploy"
set "NOTES=***"
set "STAGING=%REPO%\build\update\updates\%REV%\%VER%"
set "INSTALLERS=%REPO%\build\installers"
set "GIT_TAG=%REV%-v%VER%"

rem -------- Hostinger SFTP --------
set "SFTP_HOST=191.96.56.11"
set "SFTP_PORT=65002"
set "SFTP_USER=u637891956"

rem Hostinger's domain web root. The hPanel File Manager labels this as
rem public_html, but SSH/SFTP reaches it through domains\sigcoreuc.com.
set "SFTP_REMOTE=/home/u637891956/domains/sigcoreuc.com/public_html/pre-launch/%REV%"
set "SFTP_BATCH=%TEMP%\sigcore_sftp_%RANDOM%_%RANDOM%.txt"
set "INSTALLER_BATCH=%TEMP%\sigcore_installers_%RANDOM%_%RANDOM%.txt"
set "INSTALLER_REMOTE=/home/u637891956/domains/sigcoreuc.com/public_html/downloads"

rem ============================================================
echo(
echo =========================================
echo  SigCore UC Rev B Update
echo  Version:     %VER%
echo  Destination: PRE-LAUNCH ONLY
echo =========================================
echo(

if not exist "%BUILDER_PROJECT%" (
    echo ERROR: Required file not found:
    echo   %BUILDER_PROJECT%
    goto :failed
)
if not exist "%SIGNING_KEY%" (
    echo ERROR: Required file not found:
    echo   %SIGNING_KEY%
    goto :failed
)
if not exist "%DEPLOY_KEY%" (
    echo ERROR: Required file not found:
    echo   %DEPLOY_KEY%
    goto :failed
)

rem -------- Git: release uses all current working changes --------
echo(
echo Checking Git repository...
where git >nul 2>&1 || (
    echo ERROR: Git is not installed or is not in PATH.
    goto :failed
)
git -C "%REPO%" rev-parse --is-inside-work-tree >nul 2>&1 || (
    echo ERROR: Repository is not a Git work tree: %REPO%
    goto :failed
)
git -C "%REPO%" rev-parse -q --verify "refs/tags/%GIT_TAG%" >nul 2>&1
if not errorlevel 1 (
    echo ERROR: Git tag already exists: %GIT_TAG%
    goto :failed
)

rem -------- Shared Windows components version --------
echo(
echo Checking Control Panel version...
call "%~dp0change-control-panel-version.bat"
set "VERSION_RESULT=%ERRORLEVEL%"
if "%VERSION_RESULT%"=="1" goto :failed
if not "%VERSION_RESULT%"=="0" if not "%VERSION_RESULT%"=="2" (
    echo ERROR: Unexpected version manager return code: %VERSION_RESULT%
    goto :failed
)

rem -------- Commit ALL current Git changes for this release --------
rem Include modified, added, and deleted files; do not classify or exclude any.
echo(
echo Recording all current changes in Git...
git -C "%REPO%" add -A || goto :failed
git -C "%REPO%" diff --cached --quiet
set "GIT_DIFF_RESULT=%ERRORLEVEL%"
if "%GIT_DIFF_RESULT%"=="1" (
    git -C "%REPO%" commit -m "SigCore UC %REV% %VER% release" || goto :failed
) else if not "%GIT_DIFF_RESULT%"=="0" (
    echo ERROR: Could not inspect staged Git changes.
    goto :failed
)
set "GIT_COMMIT="
for /f "delims=" %%H in ('git -C "%REPO%" rev-parse HEAD') do set "GIT_COMMIT=%%H"
if not defined GIT_COMMIT (
    echo ERROR: Cannot determine Git commit.
    goto :failed
)
echo   Commit: %GIT_COMMIT%
echo   Tag:    %GIT_TAG%

rem -------- Shared runtime build --------
echo(
echo [1/3] Building SigCore runtime...
call build.bat || goto :failed

rem -------- Build UpdateBuilder --------
echo(
echo [2/3] Building UpdateBuilder...
dotnet build "%BUILDER_PROJECT%" -c Release -p:WarningLevel=0 --verbosity quiet || goto :failed
if not exist "%BUILDER%" (
    echo ERROR: Required file not found:
    echo   %BUILDER%
    goto :failed
)

rem -------- Build/sign update package --------
echo(
echo [3/3] Building and signing Rev B update...
"%BUILDER%" %REV% %VER% "%NOTES%" "%SIGNING_KEY%" || goto :failed

if not exist "%STAGING%\manifest.json" (
    echo ERROR: Required file not found:
    echo   %STAGING%\manifest.json
    goto :failed
)
if not exist "%STAGING%\manifest.sig" (
    echo ERROR: Required file not found:
    echo   %STAGING%\manifest.sig
    goto :failed
)
if not exist "%STAGING%\update.scu" (
    echo ERROR: Required file not found:
    echo   %STAGING%\update.scu
    goto :failed
)
if not exist "%STAGING%\update.scu.sig" (
    echo ERROR: Required file not found:
    echo   %STAGING%\update.scu.sig
    goto :failed
)

rem -------- Record the exact Git source used for this staged package --------
> "%STAGING%\git-release.txt" echo Revision: %REV%
>>"%STAGING%\git-release.txt" echo Version: %VER%
>>"%STAGING%\git-release.txt" echo Commit: %GIT_COMMIT%
>>"%STAGING%\git-release.txt" echo Tag: %GIT_TAG%
if errorlevel 1 (
    echo ERROR: Failed to write Git release metadata.
    goto :failed
)

rem -------- Stage to Hostinger via SFTP --------
echo(
echo Staging files to %SFTP_REMOTE% ...
set "STAGING_SFTP=%STAGING:\=/%"

> "%SFTP_BATCH%" echo cd %SFTP_REMOTE%
>>"%SFTP_BATCH%" echo lcd "%STAGING_SFTP%"
>>"%SFTP_BATCH%" echo put update.scu update.scu.tmp
>>"%SFTP_BATCH%" echo put update.scu.sig update.scu.sig.tmp
>>"%SFTP_BATCH%" echo put manifest.sig manifest.sig.tmp
>>"%SFTP_BATCH%" echo put manifest.json manifest.json.tmp
>>"%SFTP_BATCH%" echo rename update.scu.tmp update.scu
>>"%SFTP_BATCH%" echo rename update.scu.sig.tmp update.scu.sig
>>"%SFTP_BATCH%" echo rename manifest.sig.tmp manifest.sig
>>"%SFTP_BATCH%" echo rename manifest.json.tmp manifest.json
>>"%SFTP_BATCH%" echo bye

sftp -b "%SFTP_BATCH%" -i "%DEPLOY_KEY%" -P %SFTP_PORT% %SFTP_USER%@%SFTP_HOST% || goto :failed

if exist "%SFTP_BATCH%" del /q "%SFTP_BATCH%"

rem -------- Publish Windows installers to the live website --------
rem Each file uploads to a temporary filename first. mv -f then atomically
rem replaces the live file; directory permissions are never copied or changed.
rem Do not delete any remote files or mirror local deletions.
echo(
echo Publishing Windows installers to website...
if not exist "%INSTALLERS%\ControlPanelSetup.exe" (
    echo ERROR: Control Panel installer not found.
    goto :failed
)
if not exist "%INSTALLERS%\SigCoreCommon.exe" (
    echo ERROR: SigCoreCommon installer not found.
    goto :failed
)

rem Validate the remote directory before uploading anything.
ssh -o BatchMode=yes -i "%DEPLOY_KEY%" -p %SFTP_PORT% %SFTP_USER%@%SFTP_HOST% "test -d '%INSTALLER_REMOTE%'" || goto :failed

set "INSTALLERS_SFTP=%INSTALLERS:\=/%"
> "%INSTALLER_BATCH%" echo cd %INSTALLER_REMOTE%
>>"%INSTALLER_BATCH%" echo lcd "%INSTALLERS_SFTP%"
>>"%INSTALLER_BATCH%" echo put ControlPanelSetup.exe ControlPanelSetup.exe.uploading
>>"%INSTALLER_BATCH%" echo put SigCoreCommon.exe SigCoreCommon.exe.uploading
>>"%INSTALLER_BATCH%" echo bye
sftp -b "%INSTALLER_BATCH%" -o BatchMode=yes -i "%DEPLOY_KEY%" -P %SFTP_PORT% %SFTP_USER%@%SFTP_HOST% || goto :failed
if exist "%INSTALLER_BATCH%" del /q "%INSTALLER_BATCH%"

rem Only publish when BOTH uploads completed. Rename each file in place.
ssh -o BatchMode=yes -i "%DEPLOY_KEY%" -p %SFTP_PORT% %SFTP_USER%@%SFTP_HOST% "mv -f '%INSTALLER_REMOTE%/ControlPanelSetup.exe.uploading' '%INSTALLER_REMOTE%/ControlPanelSetup.exe' && mv -f '%INSTALLER_REMOTE%/SigCoreCommon.exe.uploading' '%INSTALLER_REMOTE%/SigCoreCommon.exe'" || goto :failed

echo   Published ControlPanelSetup.exe and SigCoreCommon.exe

rem -------- Tag only after the package was staged successfully --------
git -C "%REPO%" tag -a "%GIT_TAG%" "%GIT_COMMIT%" -m "SigCore UC %REV% %VER% staged release" || (
    echo ERROR: Package was staged, but the Git tag could not be created.
    echo Do not restage blindly. Resolve Git tagging for %GIT_TAG% first.
    goto :failed
)

rem -------- Push release commit and tag to GitHub --------
echo(
echo Pushing release to GitHub...
git -C "%REPO%" push origin main || goto :push_failed
git -C "%REPO%" push origin "%GIT_TAG%" || goto :push_failed
echo   GitHub branch: main
echo   GitHub tag:    %GIT_TAG%

echo(
echo =========================================
echo  STAGING COMPLETE
echo  Rev:     %REV%
echo  Version: %VER%
echo  Remote:  %SFTP_REMOTE%
echo  Commit:  %GIT_COMMIT%
echo  Git tag: %GIT_TAG%
echo(
echo =========================================
echo(
endlocal
exit /b 0


:push_failed
set "ERR=%ERRORLEVEL%"
if "%ERR%"=="0" set "ERR=1"
echo(
echo =========================================
echo  GITHUB PUSH FAILED
echo  The firmware was staged and installers published.
echo  The Git commit and tag exist locally.
echo  Check which push failed before retrying.
echo =========================================
echo(
endlocal & exit /b %ERR%

:failed
set "ERR=%ERRORLEVEL%"
if "%ERR%"=="0" set "ERR=1"
if exist "%SFTP_BATCH%" del /q "%SFTP_BATCH%"
if exist "%INSTALLER_BATCH%" del /q "%INSTALLER_BATCH%"
echo(
echo =========================================
echo  BUILD / STAGING FAILED
echo  Rev B update was not promoted to the live folder.
echo  If installer publishing started, check the website installers.
echo =========================================
echo(
endlocal & exit /b %ERR%

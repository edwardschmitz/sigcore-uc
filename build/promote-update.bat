@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Promote four staged Rev B files to production, using one batch file.
rem Usage: promote-update 2.0.26

set "VER=%~1"
if "%VER%"=="" goto :usage
if not "%~2"=="" goto :usage
echo(%VER%| findstr /r /x "[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*" >nul
if errorlevel 1 goto :usage

set "DEPLOY_KEY=C:\Users\eddie\OneDrive\Documents\GitHub\sigcore-uc\keys\sigcore_deploy"
set "HOST=u637891956@191.96.56.11"
set "PORT=65002"
set "ROOT=/home/u637891956/domains/sigcoreuc.com/public_html"

if not exist "%DEPLOY_KEY%" (echo ERROR: Deployment key missing.& exit /b 1)
where ssh >nul 2>&1 || (echo ERROR: OpenSSH not found.& exit /b 1)

echo(
echo Promoting SigCore UC Rev B %VER%
echo   Source: pre-launch/revB
echo   Target: update/revB ^(LIVE^)
echo(
choice /c YN /n /m "Proceed (Y/N)? "
if errorlevel 2 exit /b 1

rem All operations run directly over one SSH connection. No companion script.
rem Copy and verify temporary files first; publish manifest.json last.
ssh -o BatchMode=yes -i "%DEPLOY_KEY%" -p %PORT% %HOST% "set -eu; cd '%ROOT%'; test -d pre-launch/revB; test -d update/revB; grep -Fq '%VER%' pre-launch/revB/manifest.json; for f in update.scu update.scu.sig manifest.sig manifest.json; do test -s pre-launch/revB/$f; cp pre-launch/revB/$f update/revB/$f.promoting; cmp -s pre-launch/revB/$f update/revB/$f.promoting; done; for f in update.scu update.scu.sig manifest.sig; do mv -f update/revB/$f.promoting update/revB/$f; done; mv -f update/revB/manifest.json.promoting update/revB/manifest.json; for f in update.scu update.scu.sig manifest.sig manifest.json; do cmp -s pre-launch/revB/$f update/revB/$f; done; echo 'All four production files verified.'"
if errorlevel 1 (
  echo ERROR: Promotion failed. Check production before retrying.
  exit /b 1
)
echo SUCCESS: Rev B %VER% promoted.
exit /b 0

:usage
echo Usage: promote-update 2.0.26
exit /b 1

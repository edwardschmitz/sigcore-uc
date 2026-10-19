@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem SigCore website uploader: copy files only, never replace directory metadata.
rem Run from build, or invoke by full path from another directory.
rem Existing remote files can be overwritten. Remote files are never deleted.

for %%I in ("%~dp0..\software\Web\Website\Website\public_html") do set "SOURCE=%%~fI"
set "KEY=%~dp0..\keys\sigcore_deploy"
set "HOST=u637891956@191.96.56.11"
set "PORT=65002"
set "TARGET=/home/u637891956/domains/sigcoreuc.com/public_html"
set "BATCH=%TEMP%\sigcore_website_%RANDOM%_%RANDOM%.sftp"

if not exist "%SOURCE%\index.html" (
  echo ERROR: Missing website source: "%SOURCE%"
  exit /b 1
)
if not exist "%KEY%" (
  echo ERROR: Missing deployment key: "%KEY%"
  exit /b 1
)
where sftp >nul 2>&1
if errorlevel 1 (
  echo ERROR: Windows sftp command was not found.
  exit /b 1
)

rem Only upload files. Never use recursive scp of a directory,
rem which can overwrite directory permissions on the server.
>"%BATCH%" echo cd "%TARGET%"
if errorlevel 1 goto :generate_failed

setlocal EnableDelayedExpansion
set /a COUNT=0
rem Remote directory creation is allowed only for directories under public_html.
rem The leading dash makes "already exists" a nonfatal SFTP error.
for /d /r "%SOURCE%" %%D in (*) do (
  set "REL=%%~fD"
  set "REL=!REL:%SOURCE%\=!"
  set "REL=!REL:\=/!"
  >>"%BATCH%" echo -mkdir "%TARGET%/!REL!"
)

rem SFTP put transfers each FILE, not its containing directory.
rem No chmod, rm, rmdir, mirror, or recursive uploads.
for /r "%SOURCE%" %%F in (*) do (
  set "FULL=%%~fF"
  set "REL=!FULL:%SOURCE%\=!"
  set "REL=!REL:\=/!"
  set "LOCAL=!FULL:\=/!"
  >>"%BATCH%" echo put "!LOCAL!" "%TARGET%/!REL!"
  set /a COUNT+=1 >nul
)
>>"%BATCH%" echo bye
if !COUNT! EQU 0 (
  echo ERROR: No website files found. Nothing uploaded.
  endlocal
  goto :generate_failed
)
echo Uploading !COUNT! website files through SFTP...
echo From: %SOURCE%
echo To:   %HOST%:%TARGET%
echo Server-only files, including update/revA and ControlPanelSetup.exe, are never deleted.
echo.
endlocal

sftp -b "%BATCH%" -i "%KEY%" -P %PORT% -o BatchMode=yes -o ConnectTimeout=15 %HOST%
set "STATUS=%ERRORLEVEL%"
if exist "%BATCH%" del /q "%BATCH%" >nul 2>&1
if not "%STATUS%"=="0" (
  echo.
  echo ERROR: Upload failed. Previously uploaded files may have changed.
  exit /b %STATUS%
)
echo.
echo Website upload complete. No remote files were deleted.
exit /b 0

:generate_failed
if exist "%BATCH%" del /q "%BATCH%" >nul 2>&1
echo ERROR: Could not prepare file list. Nothing uploaded.
exit /b 1

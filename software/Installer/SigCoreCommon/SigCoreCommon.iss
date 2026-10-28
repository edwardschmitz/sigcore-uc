#ifndef WindowsVersion
  #error WindowsVersion must be passed by build.bat
#endif

; -------------------------------------------------------------
; SigCore UC - SigCoreCommon Library Installer
; -------------------------------------------------------------

[Setup]
AppName=SigCore Common Library
AppVersion={#WindowsVersion}
AppPublisher=en Z em
DefaultDirName={autopf32}\SigCore\Common
UsePreviousAppDir=no
DefaultGroupName=SigCore UC

OutputDir=..\..\..\build\installers
OutputBaseFilename=SigCoreCommon

Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyMemo=yes
PrivilegesRequired=admin

[Files]
Source: "..\..\..\build\sigcorecommon\Release\net8.0\publish\*"; \
    DestDir: "{app}"; \
    Flags: recursesubdirs ignoreversion

; Python example stored in software\libs\SigCoreCommon\Examples.
Source: "..\..\libs\SigCoreCommon\Examples\test_sigcore_io.py"; DestDir: "{app}\Examples"; Flags: ignoreversion

[Icons]
Name: "{group}\Uninstall SigCore Common Library"; \
    Filename: "{uninstallexe}"


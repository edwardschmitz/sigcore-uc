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
DefaultDirName={pf}\SigCore\Common
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

[Icons]
Name: "{group}\Uninstall SigCore Common Library"; \
    Filename: "{uninstallexe}"

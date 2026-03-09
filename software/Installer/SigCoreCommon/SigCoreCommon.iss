; -------------------------------------------------------------
; SigCore UC - SigCoreCommon Library Installer
; -------------------------------------------------------------

[Setup]
AppName=SigCore Common Library
AppVersion=1.0.50
AppPublisher=en Z em
DefaultDirName={autopf}\en-Z-em\Common
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

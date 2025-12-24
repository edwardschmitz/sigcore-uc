; -------------------------------------------------------------
; SigCore UC - Control Panel Installer
; Location: software\Installer\ControlPanel
; -------------------------------------------------------------

[Setup]
AppName=SigCore UC Control Panel
AppVersion=1.0.45
AppPublisher=en Z em
DefaultDirName={pf}\SigCore\ControlPanel
DefaultGroupName=SigCore UC

; Output folder relative to THIS .iss file
OutputDir=..\..\..\build\installers
OutputBaseFilename=ControlPanelSetup

Compression=lzma
SolidCompression=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin

[Files]
Source: "..\..\..\build\control-panel\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\SigCore UC Control Panel"; Filename: "{app}\ControlPanel.exe"
Name: "{group}\Uninstall SigCore UC Control Panel"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\ControlPanel.exe"; Description: "Launch SigCore UC Control Panel"; Flags: nowait postinstall skipifsilent

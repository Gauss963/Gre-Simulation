; Windows installer for Gre Simulation (Inno Setup 6.3+), built by GitHub Actions.
; One Setup.exe for every PC: it carries the x64 and ARM64 builds and installs the one that
; matches the machine. The question bank and pronunciation audio are shared by both and stored once.
;
; iscc /DAppVersion=1.8.1 /DX64Dir=<x64 publish dir> /DArm64Dir=<arm64 publish dir> /DOutputDir=<dir> Setup.iss

#ifndef AppVersion
  #error Pass /DAppVersion=<version>
#endif

#define AppName "Gre Simulation"
#define AppExe "GreSimulation.exe"

[Setup]
AppId={{42463645-F821-4320-869F-1CD2FAC154E5}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Gauss963
AppPublisherURL=https://github.com/Gauss963/Gre-Simulation
AppSupportURL=https://github.com/Gauss963/Gre-Simulation/issues
AppUpdatesURL=https://github.com/Gauss963/Gre-Simulation/releases/latest
VersionInfoVersion={#AppVersion}
; Per-user install into %LOCALAPPDATA%\Programs: no administrator prompt.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=Gre-Simulation-{#AppVersion}-Windows-Setup
SetupIconFile=..\src\GreSimulation.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
LZMANumBlockThreads=4
; Close a running copy when upgrading.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#X64Dir}\*"; Excludes: "\Resources\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Check: not IsArm64
Source: "{#Arm64Dir}\*"; Excludes: "\Resources\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion; Check: IsArm64
Source: "{#X64Dir}\Resources\*"; DestDir: "{app}\Resources"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

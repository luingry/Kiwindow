; Inno Setup 6. Build with scripts/build.ps1 -Installer (passes AppVersion from Directory.Build.props).
#define AppName "Kiwindow"
#ifndef AppVersion
#error Define AppVersion: run scripts/build.ps1 -Installer or ISCC /DAppVersion=x.y.z
#endif
#define AppPublisher "luingry"
#define AppUrl "https://github.com/luingry/Kiwindow"
#define AppExeName "Kiwindow.exe"
#define Bin "..\src\Kiwindow\bin\Release\net48"

[Setup]
AppId={{3B8E2C61-7F4A-4D2B-9C6E-5A1F0D8B7E42}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
; Per-user install: no UAC prompt, nothing outside the user's profile.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
OutputDir=..\artifacts\installer
OutputBaseFilename=Kiwindow-{#AppVersion}-setup
; The setup.exe itself carries the Kiwindow icon; the uninstaller entry uses the app's.
SetupIconFile=..\assets\brand\kiwindow.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
WizardStyle=modern
WizardImageFile=..\assets\brand\installer\wizard-164x314.bmp,..\assets\brand\installer\wizard-328x628.bmp
WizardSmallImageFile=..\assets\brand\installer\wizard-small-55x58.bmp,..\assets\brand\installer\wizard-small-110x116.bmp
Compression=lzma2/ultra
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ShowLanguageDialog=auto
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup

[Languages]
; English first: the default whenever Windows' display language is not Brazilian Portuguese.
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
english.LaunchApp=Open Kiwindow
brazilianportuguese.LaunchApp=Abrir o Kiwindow

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#Bin}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Bin}\{#AppExeName}.config"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#Bin}\Kiwindow.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Bin}\SwissTechKnife.Sdk.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

; Profiles in %LOCALAPPDATA%\Kiwindow are kept on uninstall (they are the user's data, shared with the
; SwissTechKnife module).

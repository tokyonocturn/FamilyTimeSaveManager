#define AppName "FamilyTime Save Manager"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{7E1B5C2A-5B7D-4B7E-9C1E-2F0B5D3A6C41}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=FamilyTime Save Manager contributors
DefaultDirName={localappdata}\Programs\FamilyTime Save Manager
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=FamilyTimeSaveManager-{#AppVersion}-setup-win-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\FamilyTimeSaveManager.exe
LicenseFile=..\LICENSE
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription=Local save manager for Family Time Episodes 1-3

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\FamilyTimeSaveManager.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\FamilyTimeSaveManager.exe"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\FamilyTimeSaveManager.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\FamilyTimeSaveManager.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

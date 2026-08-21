#define MyAppName "SoplyraAI"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "SoplyraAI"
#define MyAppExeName "SoplyraAI.exe"

[Setup]
AppId={{C2C31E92-8D8B-4B91-8A9A-0FA1C36239D1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/logeshv586-code/SoplyraAI
AppSupportURL=https://github.com/logeshv586-code/SoplyraAI/issues
AppUpdatesURL=https://github.com/logeshv586-code/SoplyraAI
DefaultDirName={autopf}\SoplyraAI
DefaultGroupName=SoplyraAI
OutputDir=dist
OutputBaseFilename=SoplyraAI-Setup
SetupIconFile=src\SoplyraAI.App\Assets\SoplyraAI.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoDescription=AI Workflow Documentation for Windows
VersionInfoProductName=SoplyraAI
VersionInfoCompany=SoplyraAI
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Files]
Source: "dist\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SoplyraAI"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\SoplyraAI"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch SoplyraAI"; Flags: nowait postinstall skipifsilent

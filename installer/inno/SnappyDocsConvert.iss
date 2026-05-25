#define MyAppName "kmb file tools"
#define MyAppExeName "SnappyDocsConvert.App.exe"

#ifndef ReleaseVersion
#define ReleaseVersion "v0.1.0-beta.1"
#endif

#ifndef AppVersion
#define AppVersion "0.1.0-beta.1"
#endif

#ifndef SourceDir
#define SourceDir "..\..\artifacts\SnappyDocsConvert-portable-win-x64-v0.1.0-beta.1"
#endif

[Setup]
AppId={{A3D64624-0673-4F16-9B07-5862DD4A4D0E}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher=KiritoMainBro88
AppPublisherURL=https://github.com/KiritoMainBro88/snappy-docs-convert
AppSupportURL=https://github.com/KiritoMainBro88/snappy-docs-convert/issues
AppUpdatesURL=https://github.com/KiritoMainBro88/snappy-docs-convert/releases
DefaultDirName={localappdata}\Programs\kmb file tools
DefaultGroupName=kmb file tools
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\..\artifacts
OutputBaseFilename=SnappyDocsConvert-setup-win-x64-{#ReleaseVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
#ifdef SetupIconFile
SetupIconFile={#SetupIconFile}
#endif
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile={#SourceDir}\LICENSE
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\kmb file tools"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall kmb file tools"; Filename: "{uninstallexe}"
Name: "{autodesktop}\kmb file tools"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch kmb file tools"; Flags: nowait postinstall skipifsilent

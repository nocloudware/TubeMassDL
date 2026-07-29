[Setup]
AppName=TubeMassDL
AppVersion=1.1.0
AppPublisher=NoCloudware
AppPublisherURL=https://www.nocloudware.com
AppSupportURL=https://github.com/nocloudware/TubeMassDL/issues
DefaultDirName={autopf}\TubeMassDL
DefaultGroupName=TubeMassDL
OutputDir=.
OutputBaseFilename=TubeMassDL-1.1.0-Setup
Compression=lzma2/max
SolidCompression=yes
UninstallDisplayIcon={app}\TubeMassDL.exe
SetupIconFile=TubeMassDL\Resources\app.ico
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
Source: "TubeMassDL\bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\TubeMassDL"; Filename: "{app}\TubeMassDL.exe"; Comment: "TubeMassDL - Link Hunter & Mass Downloader"
Name: "{autoprograms}\Uninstall TubeMassDL"; Filename: "{uninstallexe}"
Name: "{autodesktop}\TubeMassDL"; Filename: "{app}\TubeMassDL.exe"; Comment: "TubeMassDL - Link Hunter & Mass Downloader"; Tasks: desktopicon

[Run]
Filename: "{app}\TubeMassDL.exe"; Description: "Launch TubeMassDL"; Flags: postinstall nowait skipifsilent shellexec

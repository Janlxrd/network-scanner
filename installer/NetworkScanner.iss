[Setup]
AppId={{6F0C8E43-3B4A-4C1E-9D7A-5B2E8A1C4F90}
AppName=Network Scanner
AppVersion=1.0.0
AppPublisher=janiqwa
AppCopyright=Copyright (c) 2026 janiqwa
DefaultDirName={autopf}\Network Scanner
DefaultGroupName=Network Scanner
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=NetworkScanner-Setup
SetupIconFile=..\src\NetworkScanner\Assets\app.ico
UninstallDisplayIcon={app}\NetworkScanner.exe
UninstallDisplayName=Network Scanner
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\Network Scanner"; Filename: "{app}\NetworkScanner.exe"
Name: "{autodesktop}\Network Scanner"; Filename: "{app}\NetworkScanner.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NetworkScanner.exe"; Description: "Open Network Scanner"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    if MsgBox('Also delete saved devices?', mbConfirmation, MB_YESNO) = IDYES then
      DelTree(ExpandConstant('{localappdata}\NetworkScanner'), True, True, True);
end;

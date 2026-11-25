#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{6F0C8E43-3B4A-4C1E-9D7A-5B2E8A1C4F90}
AppName=Network Scanner
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
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
WizardStyle=modern dynamic
WizardImageFile=wizard.png,wizard-2x.png
WizardSmallImageFile=wizard-small.png,wizard-small-2x.png
LicenseFile=..\LICENSE

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\Network Scanner"; Filename: "{app}\NetworkScanner.exe"; Comment: "Scan your home network"
Name: "{autodesktop}\Network Scanner"; Filename: "{app}\NetworkScanner.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NetworkScanner.exe"; Description: "Open Network Scanner"; Flags: nowait postinstall skipifsilent

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{6F0C8E43-3B4A-4C1E-9D7A-5B2E8A1C4F90}_is1';

var
  ChoicePage: TInputOptionWizardPage;
  InstalledVersion: String;
  Uninstalling: Boolean;

procedure InitializeWizard();
var
  Installed, Current: Int64;
  Action: String;
begin
  if not RegQueryStringValue(HKLM64, UninstallKey, 'DisplayVersion', InstalledVersion) then exit;
  if StrToVersion(InstalledVersion, Installed) and StrToVersion('{#AppVersion}', Current) and (ComparePackedVersion(Current, Installed) > 0) then
    Action := 'Update to version {#AppVersion}'
  else if InstalledVersion = '{#AppVersion}' then
    Action := 'Repair (reinstall version {#AppVersion})'
  else
    Action := 'Install version {#AppVersion} over version ' + InstalledVersion;
  ChoicePage := CreateInputOptionPage(wpWelcome, 'Network Scanner is already installed',
    'Version ' + InstalledVersion + ' is installed on this PC.', 'What do you want to do?', True, False);
  ChoicePage.Add(Action);
  ChoicePage.Add('Uninstall Network Scanner');
  ChoicePage.SelectedValueIndex := 0;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (ChoicePage <> nil) and (PageID = wpLicense);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Uninstaller: String;
  Code: Integer;
begin
  Result := True;
  if (ChoicePage = nil) or (CurPageID <> ChoicePage.ID) or (ChoicePage.SelectedValueIndex <> 1) then exit;
  Result := False;
  if not RegQueryStringValue(HKLM64, UninstallKey, 'UninstallString', Uninstaller) then
  begin
    MsgBox('Could not find the uninstaller. Use Installed apps in Windows Settings instead.', mbError, MB_OK);
    exit;
  end;
  WizardForm.Hide;
  Exec(RemoveQuotes(Uninstaller), '', '', SW_SHOW, ewWaitUntilTerminated, Code);
  Uninstalling := True;
  WizardForm.Close;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if Uninstalling then Confirm := False;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    if MsgBox('Also delete saved devices?', mbConfirmation, MB_YESNO) = IDYES then
      DelTree(ExpandConstant('{localappdata}\NetworkScanner'), True, True, True);
end;

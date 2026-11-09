; Inno Setup Script for Kuri 32-bit Installer
; Supports Windows 10 (32-bit and 64-bit via WoW64)

#define MyAppName "Kuri"
#define MyAppVersion "1.2"
#define MyAppVersionInfo "1.2.0.0"
#define MyAppPublisher "Dynamic Notch Project"
#define MyAppURL "https://github.com/notchwin"
#define MyAppExeName "NotchWin.exe"

[Setup]
AppId={{D37E8C5F-390B-48F1-B789-98A8F24C276D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf32}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\release\installer
OutputBaseFilename=Kuri-Setup-v1.2-x86
SetupIconFile=..\NotchWin\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0.19041
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersionInfo}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} 32-bit Setup
VersionInfoProductName={#MyAppName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start Kuri quickly on Windows sign-in (scheduled task, may require Administrator approval)"; GroupDescription: "Startup options:"; Flags: unchecked

[Files]
Source: "..\NotchWin\bin\Release\net8.0-windows10.0.19041.0\win-x86\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\NotchWin\logs"

[Code]
const
  KuriRunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  KuriRunValue = 'NotchWin';

function KuriTaskRegistered(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{app}\{#MyAppExeName}'), '--autostart-task=status', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function EnableKuriAutostart(): Boolean;
var
  AppExe: String;
  ResultCode: Integer;
begin
  AppExe := ExpandConstant('{app}\{#MyAppExeName}');

  Exec(AppExe, '--autostart-task=enable', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if not KuriTaskRegistered() then
  begin
    ShellExec('runas', AppExe, '--autostart-task=enable', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;

  Result := KuriTaskRegistered();
end;

procedure DisableKuriAutostart();
var
  AppExe: String;
  ResultCode: Integer;
begin
  AppExe := ExpandConstant('{app}\{#MyAppExeName}');

  if KuriTaskRegistered() then
  begin
    Exec(AppExe, '--autostart-task=disable', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    if KuriTaskRegistered() then
    begin
      ShellExec('runas', AppExe, '--autostart-task=disable', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AppExe: String;
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('autostart') then
  begin
    if not EnableKuriAutostart() then
    begin
      AppExe := ExpandConstant('{app}\{#MyAppExeName}');
      RegWriteStringValue(HKCU, KuriRunKey, KuriRunValue, '"' + AppExe + '"');
      SuppressibleMsgBox('Kuri could not register the fast startup task, so it will start later at sign-in using the classic Run key.', mbInformation, MB_OK, IDOK);
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    DisableKuriAutostart();
    RegDeleteValue(HKCU, KuriRunKey, KuriRunValue);
  end;
end;

#define AppVersion "0.1.1"

[Setup]
AppId={{46F5E6F8-A0F2-48C8-8F28-CCDB55134FF0}
AppName=Timzoner
AppVersion={#AppVersion}
AppPublisher=Timzoner contributors
AppPublisherURL=https://github.com/jasonboi/Timzoner_win
DefaultDirName={localappdata}\Programs\Timzoner
DefaultGroupName=Timzoner
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\artifacts\installer
OutputBaseFilename=Timzoner-{#AppVersion}-win-x64-setup
SetupIconFile=..\assets\Timzoner.ico
UninstallDisplayIcon={app}\Timzoner.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
DisableProgramGroupPage=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Files]
Source: "..\artifacts\release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Timzoner"; Filename: "{app}\Timzoner.exe"; Parameters: "--settings"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Timzoner"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\Timzoner.exe"; Parameters: "--settings"; Description: "Launch Timzoner"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeUninstall(): Boolean;
var
  Code: Integer;
  Exe: String;
begin
  Result := True;
  Exe := ExpandConstant('{app}\Timzoner.exe');
  if FileExists(Exe) then
  begin
    Result := Exec(Exe, '--exit', '', SW_HIDE, ewWaitUntilTerminated, Code);
    if Result then Result := Code = 0;
    if not Result then
      SuppressibleMsgBox('Please finish any time zone change and exit Timzoner before uninstalling.', mbError, MB_OK, IDOK);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
  Exe: String;
begin
  Result := '';
  Exe := ExpandConstant('{app}\Timzoner.exe');
  if FileExists(Exe) then
  begin
    if not Exec(Exe, '--exit', '', SW_HIDE, ewWaitUntilTerminated, Code) then
      Result := 'Please exit Timzoner before updating.'
    else if Code <> 0 then
      Result := 'Timzoner is busy. Finish the time zone change and try again.';
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Prompt: String;
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
  begin
    if ActiveLanguage = 'chinesesimplified' then
      Prompt := '是否同时删除收藏时区和个人设置？选择“否”将保留设置。'
    else
      Prompt := 'Also remove favorite time zones and personal settings? Choose No to keep them.';
    if MsgBox(Prompt, mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{localappdata}\Timzoner'), True, True, True);
  end;
end;

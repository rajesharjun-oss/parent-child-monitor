#define AppName "Child Screen Time"
#define AppVersion "1.1.0"
#define AppPublisher "Child Screen Time"
#define AppExecutable "ChildScreenTime.exe"

[Setup]
AppId={{BE4296AF-72B2-4DEB-A686-D0C42D92CD81}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Child Screen Time
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=ChildScreenTime-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExecutable}
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Transparent parental screen-time controls for Windows

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExecutable}"

[Run]
Filename: "{app}\{#AppExecutable}"; Parameters: "--configure"; Description: "Configure {#AppName}"; Flags: waituntilterminated runasoriginaluser
Filename: "{sys}\sc.exe"; Parameters: "create ChildScreenTimeAgent binPath= ""{app}\ChildScreenTime.Service.exe"" start= auto obj= LocalSystem DisplayName= ""Child Screen Time Agent"""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "description ChildScreenTimeAgent ""Starts and restores Child Screen Time monitoring in the active Windows session."""; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "failure ChildScreenTimeAgent reset= 86400 actions= restart/5000/restart/10000/restart/30000"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "failureflag ChildScreenTimeAgent 1"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start ChildScreenTimeAgent"; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop ChildScreenTimeAgent"; Flags: runhidden waituntilterminated; RunOnceId: "StopChildScreenTimeService"
Filename: "{sys}\sc.exe"; Parameters: "delete ChildScreenTimeAgent"; Flags: runhidden waituntilterminated; RunOnceId: "DeleteChildScreenTimeService"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if WizardSilent then
  begin
    MsgBox('Silent installation is not supported because a parent must securely configure the initial password and limits.', mbError, MB_OK);
    Result := False;
    exit;
  end;

  if not IsWin64 then
  begin
    MsgBox('Child Screen Time requires 64-bit Windows 10 or Windows 11.', mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop ChildScreenTimeAgent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(2000);
  Exec(ExpandConstant('{sys}\sc.exe'), 'delete ChildScreenTimeAgent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
end;

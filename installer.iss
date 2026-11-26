#define MyAppName "LangFix"
#define MyAppVersion "0.6.2"
#define MyAppPublisher "Panuphong Bunkum"
#define MyAppExeName "LangFix.exe"

[Setup]
AppId={{3C47E2D4-19E4-44F0-90CF-5D88B08B1F6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\LangFix
DefaultGroupName=LangFix
OutputDir=..\outputs
OutputBaseFilename=LangFix-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
CloseApplications=force
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=Assets\LangFix.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "README.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "ThirdPartyNotices.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "ThirdParty\*.txt"; DestDir: "{app}\Licenses"; Flags: ignoreversion

[Languages]
Name: "thai"; MessagesFile: "compiler:Languages\Thai.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Icons]
Name: "{group}\LangFix"; Filename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\LangFix"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "สร้างไอคอนบน Desktop"; GroupDescription: "ไอคอนเพิ่มเติม:"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "เปิด LangFix"; Flags: nowait postinstall skipifsilent

[Registry]
; LangFix writes this "start with Windows" value itself (StartupManager). Never create it here,
; but remove it on uninstall so Windows does not try to launch a missing file at sign-in.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "LangFix"; ValueType: none; Flags: uninsdeletevalue dontcreatekey

[UninstallRun]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--shutdown"; Flags: runhidden waituntilterminated; RunOnceId: "StopLangFix"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\{#MyAppExeName}')) then
  begin
    Exec(ExpandConstant('{app}\{#MyAppExeName}'), '--shutdown', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode);
    Sleep(500);
  end;
end;

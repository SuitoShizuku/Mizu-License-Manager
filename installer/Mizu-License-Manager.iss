#ifndef AppVersion
  #define AppVersion "1.0.5"
#endif
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif
#ifdef TestBuild
  #define ProductId "Mizu.LicenseManager.InstallerTest"
  #define ProductName "Mizu License Manager Installer Test"
  #define ProductMutex "Local\MizuLicenseManagerInstallerTest"
#else
  #define ProductId "Mizu.LicenseManager"
  #define ProductName "Mizu License Manager"
  #define ProductMutex "Local\MizuLicenseManager"
#endif

[Setup]
AppId={#ProductId}
AppName={#ProductName}
AppVersion={#AppVersion}
AppPublisher=Mizu
DefaultDirName={autopf}\Mizu\Mizu-License-Manager
DefaultGroupName=Mizu
DisableProgramGroupPage=yes
WizardStyle=modern
WizardSizePercent=110
OutputDir={#OutputDir}
OutputBaseFilename=Mizu-License-Manager_Setup_v{#AppVersion}
SetupIconFile=..\Assets\Mizu-Black.ico
UninstallDisplayIcon={app}\Mizu-License-Manager.exe
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
#ifdef TestBuild
PrivilegesRequired=lowest
#else
PrivilegesRequired=admin
#endif
AppMutex={#ProductMutex}
CloseApplications=no
RestartApplications=no
UsePreviousAppDir=yes
DisableWelcomePage=no

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "startmenuicon"; Description: "スタートメニューにショートカットを作成する"
Name: "desktopicon"; Description: "デスクトップにショートカットを作成する"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
#ifndef TestBuild
Name: "{group}\Mizu License Manager"; Filename: "{app}\Mizu-License-Manager.exe"; Tasks: startmenuicon
Name: "{autodesktop}\Mizu License Manager"; Filename: "{app}\Mizu-License-Manager.exe"; Tasks: desktopicon
#endif

[Run]
#ifndef TestBuild
Filename: "{app}\Mizu-License-Manager.exe"; Description: "Mizu License Manager を起動する"; Flags: nowait postinstall skipifsilent unchecked runasoriginaluser
#endif

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#ProductId}_is1';
var
  ActionPage: TInputOptionWizardPage;
  InstalledDir, Uninstaller: String;
  Removed: Boolean;

function InstallRoot: Integer;
begin
#ifdef TestBuild
  Result := HKCU;
#else
  Result := HKLM64;
#endif
end;

procedure InitializeWizard;
begin
  RegQueryStringValue(InstallRoot, UninstallKey, 'InstallLocation', InstalledDir);
  RegQueryStringValue(InstallRoot, UninstallKey, 'UninstallString', Uninstaller);
  ActionPage := CreateInputOptionPage(wpWelcome, '操作を選択',
    'インストール・更新、修復、削除をこのファイルから実行できます。',
    'ライセンスと設定は、修復・削除しても保持されます。', True, False);
  ActionPage.Add('インストール / アップデート');
  ActionPage.Add('修復 — アプリのファイルを入れ直す');
  ActionPage.Add('削除 — アプリをアンインストールする');
  ActionPage.SelectedValueIndex := 0;
  ActionPage.CheckListBox.ItemEnabled[1] := InstalledDir <> '';
  ActionPage.CheckListBox.ItemEnabled[2] := Uninstaller <> '';
  if InstalledDir <> '' then
    WizardForm.DirEdit.Text := InstalledDir;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = wpSelectDir) and (InstalledDir <> '');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ExitCode: Integer;
begin
  Result := True;
  if (CurPageID = ActionPage.ID) and (ActionPage.SelectedValueIndex = 2) then
  begin
    Result := False;
    if MsgBox('アプリを削除しますか？' + #13#10 + InstalledDir + #13#10 +
      'ライセンスと設定はPC内に残ります。', mbConfirmation, MB_YESNO) <> IDYES then Exit;
    if CheckForMutexes('{#ProductMutex}') then
    begin
      MsgBox('通知領域のアイコンを右クリックし「終了」を選んでから再度実行してください。', mbInformation, MB_OK);
      Exit;
    end;
    if not Exec(RemoveQuotes(Uninstaller), '/SILENT /NORESTART', '', SW_SHOW,
      ewWaitUntilTerminated, ExitCode) then
    begin
      MsgBox('削除を開始できませんでした。修復してから再度お試しください。', mbError, MB_OK);
      Exit;
    end;
    if ExitCode <> 0 then
    begin
      MsgBox('削除が完了しませんでした。終了コード: ' + IntToStr(ExitCode), mbError, MB_OK);
      Exit;
    end;
    Removed := True;
    MsgBox('アプリの削除が完了しました。ライセンスと設定は保持されています。', mbInformation, MB_OK);
    WizardForm.Close;
  end;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if Removed then Confirm := False;
end;

; ShortcutBoard インストーラー定義（Inno Setup 6）
; 管理者権限不要・ユーザー単位インストール。自己完結型exe（.NET不要）を配布。

#define MyAppName "ShortcutBoard"
#define MyAppVersion "1.0.4"
#define MyAppPublisher "Personal"
#define MyAppExeName "ShortcutBoard.exe"

[Setup]
; 一意なアプリID（変更しないこと）
AppId={{B7E6F1C2-4D3A-4E9B-9C21-2F4A6B8C0D11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; 管理者権限なしでインストール（ユーザー領域へ）
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
DefaultGroupName={#MyAppName}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
; 出力先を dist フォルダに
OutputDir=..\dist
OutputBaseFilename=ShortcutBoard-Setup
SetupIconFile=..\Assets\app.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Windows 起動時に自動起動する（推奨）"; GroupDescription: "追加設定:"

[InstallDelete]
; 配布版がクリーンであることを保証するため、過去のdev/復旧ビルドが残した
; 個人データ復旧ファイルを必ず削除する（ユーザーデータ %AppData% には触れない）。
Type: files; Name: "{app}\recovery-shortcuts.json"

[Files]
Source: "..\publish_sc\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; 注意: recovery-shortcuts.json（個人データの復旧用）は配布版には同梱しない。
; 特定PCのデータ復旧が必要な場合のみ、一時的に [Files] へ追加して個別ビルドする。

[Icons]
; スタートメニュー
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
; デスクトップ（任意）
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
; 自動起動: スタートアップフォルダへ .lnk を作成
; （タスクマネージャーの「スタートアップ アプリ」に表示される。アプリ内設定と同じパス/名前）
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostart

[UninstallDelete]
; アプリ内設定から作成された .lnk も含めて確実に削除（ユーザーデータは削除しない）
Type: files; Name: "{userstartup}\{#MyAppName}.lnk"

[Run]
; インストール完了後に起動（任意）
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; \
    Flags: nowait postinstall skipifsilent

[Code]

{ ===== 起動中プロセスの検知・自動終了 =====
  ShortcutBoard は「閉じる」を Hide で握りつぶすため WM_CLOSE では終了しない。
  そのため確実に /F（強制終了）で閉じる。アプリ側は編集の都度アトミック保存し
  バックアップも持つため、強制終了してもユーザーデータは失われない。 }

function IsAppRunning(): Boolean;
begin
  { アプリの単一起動用ミューテックスで起動中かを判定 }
  Result := CheckForMutexes('Global\ShortcutBoard_SingleInstance_Mutex');
end;

procedure TerminateRunningApp();
var
  ResultCode, i: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM ShortcutBoard.exe',
       '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  { プロセス終了とファイルロック解放を待つ（最大約5秒） }
  for i := 1 to 20 do
  begin
    if not IsAppRunning() then break;
    Sleep(250);
  end;
  Sleep(1000); { exe ハンドルが解放される猶予 }
end;

{ ファイルコピー直前に呼ばれる。ここで起動中の exe を確実に閉じておくことで
  DeleteFile / 上書きの「アクセスが拒否されました（コード5）」を防ぐ。 }
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';

  if IsAppRunning() then
  begin
    if not WizardSilent() then
      MsgBox('ShortcutBoard が起動中です。' + #13#10 +
             '更新を続けるため、いったん自動的に終了します。',
             mbInformation, MB_OK);
    TerminateRunningApp();
  end;

  { それでも終了できなかった場合は、手動終了を案内して中断 }
  if IsAppRunning() then
    Result := 'ShortcutBoard を自動的に終了できませんでした。' + #13#10 + #13#10 +
              '通知領域（タスクバー右下の「∧」内）の青い「S」アイコンを右クリックし、' + #13#10 +
              '「終了」を選んでアプリを閉じてから、もう一度このインストーラーを実行してください。';
end;

[UninstallRun]
; アンインストール時に起動中のプロセスを終了
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; \
    Flags: runhidden; RunOnceId: "KillShortcutBoard"
; 旧バージョンが使っていたレジストリRun値も掃除（存在しなくてもエラーにしない）
Filename: "{cmd}"; Parameters: "/C reg delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v ShortcutBoard /f & exit 0"; \
    Flags: runhidden; RunOnceId: "RemoveLegacyRunValue"

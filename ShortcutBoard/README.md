# ShortcutBoard

自分で設定したカスタムショートカットを忘れたとき、**グローバルショートカットキーを押すだけ**で画面中央に一覧を表示できる、個人用の Windows 常駐アプリです。

- **技術**: C# / .NET 8 / WPF（Electron不使用・有料ライブラリ不使用）
- **動作**: 管理者権限なしで動作。通知領域（システムトレイ）に常駐。
- **既定ホットキー**: `Ctrl + Alt + Space`

---

## 主な機能

- グローバルホットキー（既定 `Ctrl + Alt + Space`）でどのアプリからでも一覧を表示
- ウィンドウ外クリック / `Esc` で非表示（アプリは終了せずトレイに常駐）
- ショートカットキー / 名前 / 説明 / カテゴリーの一覧表示（横長カード形式・ダークモード）
- キーワード検索・カテゴリー絞り込み
- ショートカットの追加・編集・削除（未保存時は確認ダイアログ）
- データは JSON でローカル保存：`%AppData%\ShortcutBoard\shortcuts.json`
- 初回起動時はサンプルデータを自動投入
- JSON 破損時はバックアップを作成しサンプルで自動復旧
- 設定画面からホットキー変更 / Windows 自動起動の切り替え
- 二重起動防止（Mutex）、例外でアプリ全体が落ちない設計、ログ出力

### キーボード操作
| キー | 動作 |
|------|------|
| `Ctrl + Alt + Space` | 一覧を表示（グローバル） |
| `↑` / `↓` | 項目を移動 |
| `Enter` / ダブルクリック | 選択中のショートカットキーをクリップボードへコピー |
| `Esc` | 一覧を非表示 |

---

## 動作要件

- Windows 10 / 11（x64）
- [.NET 8 デスクトップ ランタイム](https://dotnet.microsoft.com/download/dotnet/8.0)（配布版 exe を動かすために必要）

---

## 実行方法

### 1. 開発時に実行する
```powershell
dotnet run --project ShortcutBoard\ShortcutBoard.csproj
```

### 2. ビルドする
```powershell
dotnet build ShortcutBoard\ShortcutBoard.csproj -c Release
```

---

## 配布用 exe を作成する

### A. 単一 exe（フレームワーク依存・推奨 / 軽量）
実行先の PC に「.NET 8 デスクトップ ランタイム」がある前提。出力は数百KB程度。

```powershell
dotnet publish ShortcutBoard\ShortcutBoard.csproj `
  -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ShortcutBoard\dist
```

→ `ShortcutBoard\dist\ShortcutBoard.exe` が生成されます。

### B. 単一 exe（自己完結型 / ランタイム不要・サイズ大）
.NET ランタイムが入っていない PC でも動かしたい場合（約 150MB）。

```powershell
dotnet publish ShortcutBoard\ShortcutBoard.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o ShortcutBoard\dist-selfcontained
```

> いずれも初回実行時に `%AppData%\ShortcutBoard` 配下へデータ・ログが作成されます。

---

## 配布（友人に渡す）

`dist\ShortcutBoard-Setup.exe` が**配布用インストーラー（v1.0.1 安定版）**です。
**この1ファイルだけ**を友人に渡してください（.NET ランタイム不要・自己完結型）。

> **安定版ステータス（v1.0.1）**：実機で**PC再起動後も常駐し、登録データが保持される**ことを確認済み。
> 起動時にデータが一時的に読めない異常が起きても、多層バックアップ（`shortcuts.backup.json`／
> 日次スナップショット）から自動復元し、サンプルデータで上書きしない設計です。
> 配布用インストーラーには**個人データを同梱していません**（新規ユーザーはサンプルデータで開始）。

### 友人側の手順（かんたん）
1. `ShortcutBoard-Setup.exe` をダブルクリック
2. 「WindowsによってPCが保護されました」と出たら **「詳細情報」→「実行」**
   （個人作成で署名がないための警告です）
3. ウィザードを進める。途中の **「Windows 起動時に自動起動する」** にチェックすると常駐が自動化されます
4. 完了で起動。初回に「**Ctrl + Shift + A** で一覧を表示できます」と案内が出ます

- 起動：スタートメニュー →「ShortcutBoard」、または PC 起動時に自動（上記チェック時）
- 使い方：`Ctrl + Shift + A` で一覧表示／`Esc`・外側クリックで非表示／通知領域に常駐
- アンインストール：Windows「設定」→「アプリ」→「ShortcutBoard」→「アンインストール」

> 友人向けの説明書きは `dist\はじめにお読みください.txt` にも同梱しています。

### 旧版を渡した人向け：最新版への入れ替え手順
**アンインストール不要・データ保持・常駐中でもOK**で上書き更新できます（同じ AppId のため在席更新）。

> v1.0.2 で、**起動中(常駐中)に上書きしても DeleteFile エラー（コード5）にならない**よう、
> インストーラーが起動中の ShortcutBoard を自動終了してから更新するようにしました。

1. 新しい `ShortcutBoard-Setup.exe` を実行（「詳細情報」→「実行」）。常駐中でもそのままでOK
   （自動で終了→更新します。自動終了できない場合のみ「S」→「終了」して再実行）
2. 完了後、通知領域の「S」→「設定」で **v1.0.2** になっていれば更新成功
   - タスクマネージャーの「スタートアップ アプリ」に **ShortcutBoard** が表示されることも確認可
4. 旧版が使っていたレジストリ Run 登録は、更新時に自動でスタートアップフォルダ方式へ移行・掃除されます

> 既存の登録データ（`%AppData%\ShortcutBoard`）はアンインストールしても残るため、
> 万一の再インストールでも引き継がれます。

### インストーラーを作り直す（開発者向け）
[Inno Setup 6](https://jrsoftware.org/isdl.php) が必要です。

```powershell
# 1) 自己完結型・単一exeを発行（dist には含めず publish_sc に作る）
dotnet publish ShortcutBoard\ShortcutBoard.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o ShortcutBoard\publish_sc

# 2) インストーラーをコンパイル（→ dist\ShortcutBoard-Setup.exe）
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" ShortcutBoard\installer\ShortcutBoard.iss
```

インストーラー定義は `installer\ShortcutBoard.iss`。管理者権限不要（ユーザー領域へ
インストール）、スタートメニュー登録・任意の自動起動・アンインストール対応です。

---

## データ・ログの保存場所

| 種類 | パス |
|------|------|
| ショートカット | `%AppData%\ShortcutBoard\shortcuts.json` |
| 設定 | `%AppData%\ShortcutBoard\settings.json` |
| ログ | `%AppData%\ShortcutBoard\logs\shortcutboard-YYYYMMDD.log` |
| 破損時バックアップ | `…\shortcuts.json.corrupted-YYYYMMDD-HHmmss.bak` |

`%AppData%` は通常 `C:\Users\<ユーザー名>\AppData\Roaming` です。

---

## ホットキーの変更方法

1. 通知領域の ShortcutBoard アイコンを右クリック →「設定」
2. 「グローバルホットキー」のボックスをクリック
3. 設定したいキーの組み合わせ（修飾キー `Ctrl/Alt/Shift/Win` ＋ 任意のキー）を押す
4. 「OK」で保存（即座に再登録されます）

> 既定の `Ctrl + Alt + Space` が他アプリと競合している場合は、起動時にトレイから
> 「ホットキー登録に失敗」の通知が出ます。設定から別のキーに変更してください。

---

## Windows 自動起動の設定方法

1. 通知領域アイコンを右クリック →「設定」
2. 「Windows 起動時に自動起動する」にチェック →「OK」

内部的には管理者権限不要のレジストリ
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` に登録/解除します。

---

## 構成（責務分離 / MVVM）

```
ShortcutBoard/
├─ Models/        … Shortcut, AppSettings
├─ ViewModels/    … Main / Edit / Settings, ObservableObject
├─ Views/         … MainWindow / EditWindow / SettingsWindow
├─ Services/      … Json保存 / グローバルホットキー / トレイ / 自動起動 / ログ
├─ Data/          … サンプルデータ
├─ Helpers/       … パス / RelayCommand / Win32 / ホットキー変換
└─ Themes/        … ダークテーマ
```

主要サービスはすべて独立クラスに分離しています（`JsonStorageService` /
`HotkeyService` / `TrayIconService` / `StartupService` / `LogService`）。
外側クリック検知はメイン一覧ウィンドウ側で、子ウィンドウ・コンボ操作・トレイメニュー
表示中は自動非表示を抑制するカウンタ方式で実装しています。

---

## 今後追加できそうな機能

- ショートカットのインポート / エクスポート（JSON 共有）
- 一覧から直接アプリ/URL を起動するアクション
- お気に入り / 使用頻度順の並び替え
- グローバル検索（あいまい検索・ローマ字対応）
- 複数ホットキー（カテゴリーごとに別キーで開く）
- テーマ切り替え（ライト/ダーク）・フォントサイズ調整
- クラウド同期（OneDrive 等のフォルダを保存先に指定）

---

## macOS 版（Avalonia）

Windows 版（WPF）と同等機能の macOS 版を追加しています。共通ロジックは
`ShortcutBoard.Core` に集約し、UI/常駐/ホットキーのみ OS 別に実装しています。

### プロジェクト構成
| プロジェクト | 役割 | TFM |
|------|------|-----|
| `ShortcutBoard.Core` | Models / 保存(JSON) / ログ / 検索・並べ替え（共通） | net8.0 |
| `ShortcutBoard` | Windows 版 UI（WPF） | net8.0-windows |
| `ShortcutBoard.Mac` | macOS 版 UI（Avalonia, メニューバー常駐） | net8.0 |
| `ShortcutBoard.Tests` | Core の単体テスト（xUnit, Win/Mac 両方で実行） | net8.0 |

### ビルド／配布（GitHub Actions のみで完結。Mac 実機不要）
`.github/workflows/build.yml` が以下を自動実行します。
- **windows ジョブ**：Core 単体テスト＋WPF ビルド（Windows 版の維持）
- **macos ジョブ（Apple Silicon ランナー）**：
  - Core 単体テスト
  - `osx-arm64`（Apple Silicon）/ `osx-x64`（Intel）を **自己完結型**で発行
  - `.app` バンドル＋`.dmg` を生成（`build/mac/make-bundle.sh`）
  - セルフテスト（設定・一覧の読み書き）と起動確認（クラッシュしないこと）
  - `.dmg` を Artifacts としてアップロード

### 成果物のダウンロード
GitHub の **Actions** → 対象の実行 → **Artifacts** の `ShortcutBoard-macOS-dmg` を取得。
中に `ShortcutBoard-AppleSilicon.dmg` と `ShortcutBoard-Intel.dmg` が入っています。

### macOS 版の保存先
`~/Library/Application Support/ShortcutBoard`（アンインストール後もデータは残ります）

### 署名・公証（あとから追加）
未署名のため初回は「開発元を確認できません」と出ます（右クリック →「開く」で起動）。
`build/mac/make-bundle.sh` 内のコメント箇所に `codesign` / `notarytool` を追記すれば
署名・公証に対応できます。

# ShortcutBoard — MSIX 試作メモ（Microsoft Store 用）

> 目的：Store配布用の MSIX を**無料の範囲**で試作する。
> 既存の GitHub Release（v1.0.4 / `ShortcutBoard-Setup.exe` / `release.yml` / `build.yml`）は一切変更しない。
> Store配布(MSIX) と GitHub配布(Inno Setup .exe) は**併用**する。

## Partner Center 取得値（このパッケージの固定値）
| 項目 | 値 |
|---|---|
| Package Identity Name | `E647CFB5.ShortcutBoard` |
| Publisher | `CN=42DACE4F-C681-4106-89D1-B5D746E48DE1` |
| PublisherDisplayName | `地獄スペシャル` |
| Package Family Name | `E647CFB5.ShortcutBoard_ctghycb4dd3wm` |
| Microsoft Store ID | `9NZ1GZT2H699` |

→ `packaging/msix/AppxManifest.xml` の `Identity` / `Properties` に反映済み。

---

## 1. 最小構成（採用方針）
**`.wapproj`（Visual Studio の Windows Application Packaging Project）は使わない。**
理由：`.wapproj` は Visual Studio / MSBuild のパッケージングターゲットが必要で、`dotnet` CLI 単体や
GitHub Actions では扱いにくい。代わりに **「自己完結 publish ＋ `AppxManifest.xml` ＋ `MakeAppx pack`」** 方式を採用。

```
packaging/msix/
├─ AppxManifest.xml      … 完全信頼デスクトップアプリ定義（Identity は予約値）
├─ Assets/               … Store/タイル用ロゴ（Square44 / Square150 / StoreLogo）
├─ build-msix.ps1        … ローカル/CI 共通ビルド（publish→manifest差込→MakeAppx）
└─ layout/ , *.msix      … 生成物（.gitignore 済み・コミットしない）
```

### `ShortcutBoard.Package` プロジェクトは必要か？ → **不要**
- 上記 MakeAppx 方式なら新規 C# プロジェクト（.wapproj/.csproj）は不要。
- ソリューション構成・本体コードは無変更のまま MSIX 化できる。

---

## 2. 本体コード変更の有無
**今回の最小試作では本体コード変更は不要（0行）。**
- 既存の自己完結 publish 出力をそのまま MSIX に同梱し、`Executable="ShortcutBoard.exe"` /
  `EntryPoint="Windows.FullTrustApplication"` で完全信頼起動する。
- グローバルホットキー / トレイ常駐 / 単一起動 / 検索・編集・保存は完全信頼パッケージでそのまま動作。

> 唯一あとで必要になるのは **自動起動**（下記§3）。これは本体コード変更を伴うため、
> **今回は実装せず方針のみ**。実装着手前に理由・影響範囲を別途説明する。

---

## 3. 自動起動方式の違い（GitHub版 vs Store/MSIX版）
| | GitHub版（現行） | Store/MSIX版（将来対応） |
|---|---|---|
| 方式 | スタートアップフォルダに `.lnk` 作成（`StartupService`） | `windows.startupTask` マニフェスト拡張 ＋ `StartupTask` API |
| 実装 | 実装済み・動作中 | 未実装 |
| MSIXでの現行コードの挙動 | パッケージ環境では `.lnk` 書き込みが仮想化/不発になり得る（try/catchで**クラッシュはしない**が、自動起動は機能しない可能性） | 正式対応が必要 |

### 将来の実装方針（着手は別途承認後）
1. `AppxManifest.xml` の `<Application>` に `uap5:Extension Category="windows.startupTask"` を追加（Id・DisplayName・有効/無効既定）。
2. 本体側で「パッケージ実行かどうか」を判定して分岐：
   - パッケージ時：`Windows.ApplicationModel.StartupTask` API で有効/無効を制御（`StartupService` をインターフェース化し Mac/Win/MSIX 実装を差し替え）。
   - 非パッケージ時：現行の `.lnk` 方式を維持。
   - 判定例：`Windows.ApplicationModel.Package.Current` 参照が例外なら非パッケージ。
3. 影響範囲：`ShortcutBoard`（WPF）の `StartupService` 周辺のみ。Core/Mac/既存配布には影響させない。

> **今回の試作では §3 は未実装**。MSIX が「起動して常駐する」ことの確認を最優先（要件4）。
> 試作版では設定の「自動起動」トグルは Store 版で効かない場合がある旨を、提出時の説明に記載する。

---

## 4. データ保存（`%AppData%\ShortcutBoard`）
- パッケージ版は別サンドボックスのため、**GitHub版とデータは共有されない**（それぞれ独立して永続化）。
- 永続化自体は動作する。Store版を入れても GitHub版のデータは消えない（場所が別なだけ）。
- 多層バックアップ/自動復元ロジックはそのまま有効。

---

## 5. ビルド方法

### ローカル（Windows SDK がある環境）
```powershell
# Store提出用（署名なし）
powershell -File packaging\msix\build-msix.ps1 -Version 1.0.4
# ローカル実機テスト用（無料の自己署名・ローカル限定。Store提出には不要）
powershell -File packaging\msix\build-msix.ps1 -Version 1.0.4 -Sign
```
> 自己署名は**無料**。インストールするには生成した証明書を「信頼されたユーザー/ルート」に追加する必要がある（ローカル検証専用）。

### GitHub Actions（推奨・ローカルにSDK不要）
- ワークフロー：`.github/workflows/msix.yml`（`workflow_dispatch`・手動実行）
- 手順：Actions → **msix** → Run workflow → `version`（例 1.0.4）
- 成果物：Artifacts の **`ShortcutBoard-msix`**（**署名なし `.msix`** = Store にそのままアップロード可）
- windows-latest には Windows SDK（MakeAppx）があるため CI 単独で生成可能。

> 署名なし `.msix` はローカルではインストールできないが、**Store へのアップロードは可能**（Microsoft が署名する）。

---

## 6. Store 審査で問題になりそうな点
- `runFullTrust` 宣言（デスクトップアプリでは標準・許可）。審査ノートは出るが通る想定。
- 自動起動：`.lnk` のままだと不適切と見なされ得る → §3 の `startupTask` 化が本提出前の宿題。
- プライバシーポリシー URL 必須（通信なしでも要求されがち）→ `docs/store-listing-draft.md` に本文下書きあり。無料ホスト（GitHub Pages 等）で公開。
- スクリーンショットのサイズ規定・個人情報の写り込みに注意。

---

## 7. Store 提出前チェックリスト
- [ ] `version` を決めて MSIX をビルド（CI: msix.yml）→ Artifacts に `.msix` が出る
- [ ] （任意）ローカルで `-Sign` し実機インストール → **起動・常駐・Ctrl+Shift+A・編集・保存**を確認
- [ ] `AppxManifest.xml` の Identity/Publisher/PublisherDisplayName が Partner Center 値と完全一致
- [ ] Version の第4桁が `.0`（例 `1.0.4.0`）
- [ ] 価格 = **Free**、アプリ内課金なし
- [ ] **自前コード署名証明書を求められない**こと（MSIX は Microsoft が署名）
- [ ] **登録・提出で料金が請求されない**こと（出たら中止）
- [ ] プライバシーポリシーURL / サポートURL が有効
- [ ] スクリーンショット（規定サイズ・個人情報の写り込みなし）
- [ ] 年齢レーティング（IARC）回答：収集なし・通信なし → 全年齢想定
- [ ] 自動起動が Store 版で未対応である点を、説明文/自分用メモに反映（§3 を実装するまで）
- [ ] 既存 GitHub Release / Setup.exe / release.yml に影響していないこと

---

## 8. PublisherDisplayName 「地獄スペシャル」の見え方（要確認）
- `PublisherDisplayName` は **Store のアプリページや Windows の「設定 > アプリ」で“発行元/開発者名”としてユーザーに表示される**値です。
- つまり一般ユーザーには **「地獄スペシャル」が発行元名として公開されます**。
- これは Partner Center の**アカウントの発行元表示名**と一致している必要があり、提供値はその予約値なので整合します。
- 注意点：
  - 実名ではなくブランド/ハンドル名（地獄スペシャル）が表示される点は**プライバシー的にはむしろ安全**（実名・住所は Microsoft への本人確認用で、一般公開ページには出ない）。
  - ただし「この名前で公開されてよいか」はブランディングの判断。変えたい場合は **Partner Center 側のアカウント発行元表示名を変更 → マニフェストの `PublisherDisplayName` も一致させて再ビルド** が必要。
  - 多言語ストアでもこの文字列がそのまま出る（地獄スペシャル のまま）。英語圏ユーザーにも日本語表記で表示される点は許容するか検討。

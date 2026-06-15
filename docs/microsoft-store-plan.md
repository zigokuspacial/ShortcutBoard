# ShortcutBoard — Microsoft Store 公開プラン（無料の範囲）

> 方針：**無料でできる範囲のみ**。有料登録・証明書購入・Azure有料契約・課金設定はしない。
> 既存の GitHub Release（v1.0.4 / `ShortcutBoard-Setup.exe` / `release.yml`）はそのまま維持し、
> **Store配布とGitHub配布を併用**する前提。本ドキュメントは調査・方針のみ（実提出はしない）。

最終確認日: 2026-06（下部「出典」参照）

---

## 1. 結論：無料で進められるか？

**ほぼ全工程を無料で進められます。** 唯一お金がかかり得たのは「個人開発者登録料」と「コード署名証明書」でしたが、

- 🟢 **個人開発者の登録料は 2026年時点で無料**（新オンボーディングで個人・法人とも登録料なし。約200市場対応）。本人確認（政府発行ID＋セルフィー）は必要だが**費用なし**。
- 🟢 **コード署名証明書は不要**：**MSIX を Store に提出すると、審査通過後に Microsoft が自動で署名**してくれる。自前の有料証明書（年1万円〜）も、USBトークンも不要。
  - ⚠️ ただし **EXE/MSI を Store に出す場合は Microsoft は署名してくれず、自前の有料 Authenticode 署名が必須**。→ なので **Store 用は MSIX 一択**（既存の Inno Setup `.exe` は Store には使わず、GitHub配布のまま残す）。

### 有料になり得るポイント（＝今回は踏まない）
| 項目 | 費用 | 本プランでの扱い |
|---|---|---|
| 個人開発者登録 | **無料**（2026〜） | 進める（本人確認のみ） |
| MSIX を Store 提出時の署名 | **無料**（MSがやる） | これを使う |
| 自前コード署名証明書 | 有料（年額〜） | **使わない** |
| EXE/MSI を Store 提出 | 自前署名必須＝実質有料 | **やらない**（MSIXで出す） |
| Azure / クラウド | 有料あり | **使わない**（不要） |
| プライバシーポリシー / サポートURL ホスティング | **無料**（GitHub Pages / リポジトリで可） | 無料で用意 |
| 年齢レーティング(IARC) | **無料** | 質問票に回答するだけ |

### 発行元（Publisher）表示名の注意
- 個人アカウントでも **「発行元の表示名（Publisher display name）」は自分で設定**できる（公開ページに出るのはこの表示名）。
- ただし**本人確認では実名・住所等が必要**（Microsoftに対しては実名提出。一般公開ページに住所が出るわけではない）。
- プライバシー重視なら、**表示名にフルネームや個人特定情報を入れない**（例：`ShortcutBoard` や任意のハンドル名）。登録時に「公開ページで何が表示されるか」を必ず確認する。
- 連絡先メールが公開設定に含まれる箇所があるため、**専用の連絡用メール**を使うのが無難。

---

## 2. Store 提出に必要なもの（チェックリスト）

| # | 項目 | 無料か | 用意状況 / メモ |
|---|---|---|---|
| 1 | Partner Center 個人開発者アカウント | 無料 | 未登録（本人確認が必要） |
| 2 | アプリ名の予約（Reserve name）「ShortcutBoard」 | 無料 | 登録後に予約。**取得できれば Package Identity / Publisher 値が確定** |
| 3 | MSIX パッケージ（`.msix`） | 無料で作成可 | 未作成（§3-4 参照） |
| 4 | Store 掲載文（短文・詳細・キーワード） | 無料 | `docs/store-listing-draft.md` に下書き済み |
| 5 | スクリーンショット（1枚以上、推奨 1366×768 等） | 無料 | `docs/images/main.png` `edit.png` あり（Store規定サイズに調整要） |
| 6 | プライバシーポリシー URL | 無料 | GitHub Pages か リポジトリの md で用意（§5 候補） |
| 7 | 年齢レーティング（IARC 質問票） | 無料 | 提出時に回答（ツール系・収集なし → 全年齢想定） |
| 8 | サポート URL / 連絡先 | 無料 | GitHub の Issues / リポジトリURL |
| 9 | 価格 = **無料(Free)** に設定 | 無料 | 提出時に「Free」を選ぶだけ |

> 名前予約（#2）をすると Partner Center が **Package/Identity Name・Publisher・Publisher display name** を割り当てる。
> MSIX マニフェスト（`Package.appxmanifest`）の `Identity` をこの値に合わせる必要がある。

---

## 3. MSIX 化の実現可能性（WPF / .NET）

**可能。** WPF/.NET 8 アプリは「パッケージ化された完全信頼（full-trust packaged）デスクトップアプリ」として MSIX 化でき、Store も受け付ける。

### 作り方の選択肢
| 方法 | 無料 | 備考 |
|---|---|---|
| A. Visual Studio + **Windows Application Packaging Project (.wapproj)** | VS Community 無料 | 最も素直。WPFプロジェクトを参照に追加してパッケージ化。ローカル実機がMacのため今すぐは不可（要Windows） |
| B. **MSIX Packaging Tool**（既存インストーラをキャプチャ） | 無料 | 既存 `Setup.exe` を取り込む方式。手早いが startupTask 等の最適化はしにくい |
| C. **MakeAppx.exe（Windows SDK）＋手書き manifest** | 無料 | CI 向き。`dotnet publish` 出力＋`AppxManifest.xml` を `MakeAppx pack` で MSIX 化 |
| D. GitHub Actions で C を自動化 | 無料 | windows ランナーに SDK あり。**ローカル実機なしでも MSIX を生成可能**（Store 提出用は署名不要） |

→ 推奨：**まず C/D（CIでMSIX生成）を試作**。署名は Store 提出時は不要。**ローカル実機テスト時だけ自己署名証明書（無料・ローカル限定）** が要る点に注意。

### 各機能の MSIX 環境での可否
| 機能 | MSIX での状況 | 対応 |
|---|---|---|
| グローバルホットキー（`RegisterHotKey`） | 🟢 full-trust パッケージで動作 | 変更不要 |
| トレイ常駐（WinForms `NotifyIcon`） | 🟢 動作 | 変更不要 |
| 単一起動（Mutex） | 🟢 動作 | 変更不要（Global mutex 可） |
| **ログイン時自動起動** | 🟠 **現在の「スタートアップフォルダ .lnk」方式は MSIX では非推奨/不可** | **要対応**：MSIX は `windows.startupTask` マニフェスト拡張＋`StartupTask` API を使う。パッケージ実行時のみそちらを使う分岐が必要 |
| データ保存 `%AppData%\ShortcutBoard` | 🟠 動作するが**保存先が分離**される可能性 | パッケージ版データは GitHub版と**共有されない**（別サンドボックス）。永続化自体は問題なし。要明記 |
| 初回オンボーディング / 編集 / 検索 等 | 🟢 影響なし | 変更不要 |

### Store 審査で問題になりそうな点
- **完全信頼アプリ宣言**：`runFullTrust` 制限付き機能を宣言（デスクトップアプリでは標準・許可される）。審査ノートは出るが通る。
- **自動起動**：`.lnk`方式のままだと「不適切なスタートアップ登録」と見なされ得る → **startupTask 拡張に置き換える**のが安全。
- **プライバシーポリシー必須**：通信しなくても URL 提出を求められることが多い → 用意する（無料）。
- **機能の明確さ**：スクショ・説明が実機能と一致していること（規約上の基本）。
- **未署名のままアップロード可**だが、`Identity` を予約済みの値に合わせること（不一致だと弾かれる）。

---

## 4. MSIX 試作の進め方（段階的・既存を壊さない）

> 大規模変更はしない。既存 Inno Setup 配布・`release.yml`・v1.0.4 はそのまま。

1. **（無料・登録不要で今できる）** マニフェスト雛形 `packaging/msix/AppxManifest.template.xml` を用意（Identity はプレースホルダ）。本体コードは変更しない。
2. **アプリ名予約後**：割り当てられた Identity/Publisher をマニフェストに反映。
3. **自動起動だけ条件分岐を実装**（パッケージ実行時は `StartupTask` API、非パッケージ時は現行 .lnk）。
   - パッケージ判定：`Windows.ApplicationModel.Package.Current`（非パッケージだと例外）で分岐。
   - これが唯一の本体コード変更ポイント。**今回はやらず、方針のみ記載**。
4. **CI で MSIX 生成**：`build.yml`/`release.yml` とは別の `msix.yml`（将来）。`dotnet publish` → `MakeAppx pack` → アーティファクト。**今回は作らない**。
5. **ローカル検証**：自己署名証明書（無料・ローカル限定）で署名して実機インストール確認 → Store 提出は署名なしでOK。
6. **Store 提出**：Partner Center で MSIX アップロード → 掲載文・スクショ・年齢レーティング・価格Free → 審査。

### 併用方針
- **GitHub Release（Inno Setup .exe）**：上級者・最新版を即配布。現状維持。
- **Microsoft Store（MSIX）**：一般ユーザー向け。**SmartScreen警告が出ず、Microsoftが署名**するため安心感が高い。
- データは別管理になる旨を README に将来追記（今回はしない）。

---

## 5. 公開に使う無料URL候補
- **サポートURL**：`https://github.com/zigokuspacial/ShortcutBoard`（または Issues）
- **プライバシーポリシーURL**：
  - 案A：リポジトリに `PRIVACY.md` を置き Raw/GitHub表示URL
  - 案B：GitHub Pages（無料）で `privacy.html`
  - 内容：「通信なし・データはローカル（`%AppData%\ShortcutBoard`）保存・収集や送信なし」

---

## まとめ（要点）
- ✅ **登録もStore公開も、今は無料で進められる**（個人登録料が2026年に撤廃、MSIXはMSが署名）。
- ⚠️ **有料化ポイント**：自前コード署名証明書（→使わない）、EXE/MSIをStore提出（→使わない）、Azure（→使わない）。踏まなければ$0。
- 🟢 **実現可能性は高い**（WPF→MSIXは標準対応）。
- 🟠 **唯一の要対応**：自動起動を MSIX 用に `startupTask` 方式へ分岐（小規模なコード変更。今回は方針のみ）。
- 🟠 データはStore版とGitHub版で分離される点に留意。

---

## 出典
- [Free developer registration for individual developers (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/apps/publish/whats-new-individual-developer)
- [Steps to open a Microsoft Store developer account in Partner Center](https://learn.microsoft.com/en-us/windows/apps/publish/partner-center/open-a-developer-account)
- [App package requirements for MSIX app](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements)
- [Code signing options for Windows app developers](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)
- [Q&A: Will Microsoft sign an unsigned .msix submitted to the Store?](https://learn.microsoft.com/en-us/answers/questions/3935727/i-have-an-unsigned-app-msix-file-it-has-been-submi)

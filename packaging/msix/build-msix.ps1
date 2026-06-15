<#
  ShortcutBoard MSIX ビルドスクリプト（試作）
  - WPF/.NET アプリを自己完結publishし、AppxManifest.xml と Assets を重ねて MakeAppx で .msix 化する。
  - 既存の Inno Setup 配布(ShortcutBoard-Setup.exe)には一切触れない。
  - Store 提出用は「署名なし .msix」でOK（Microsoft が署名する）。
  - ローカル実機テストでインストールしたいときだけ -Sign（無料の自己署名・ローカル限定）を使う。

  使い方:
    # Store提出用（署名なし）
    powershell -File packaging\msix\build-msix.ps1 -Version 1.0.4
    # ローカル検証用（無料の自己署名で署名し、その場でインストールできる形に）
    powershell -File packaging\msix\build-msix.ps1 -Version 1.0.4 -Sign
#>
param(
  [string]$Version = "1.0.4",
  [switch]$Sign
)
$ErrorActionPreference = "Stop"

# リポジトリ ルートを基準にする（このスクリプトは packaging\msix にある想定）
$root      = (Resolve-Path "$PSScriptRoot\..\..").Path
$proj      = Join-Path $root "ShortcutBoard\ShortcutBoard.csproj"
$assets    = Join-Path $PSScriptRoot "Assets"
$manifest  = Join-Path $PSScriptRoot "AppxManifest.xml"
$layout    = Join-Path $root "packaging\msix\layout"
$outMsix   = Join-Path $root "packaging\msix\ShortcutBoard.msix"

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version は 1.0.4 形式で指定してください" }
$pkgVersion = "$Version.0"   # MSIX は4桁・リビジョンは0固定

# 1) 自己完結publish（単一ファイルにはしない＝MSIX はばらのファイル構成）
if (Test-Path $layout) { Remove-Item -Recurse -Force $layout }
New-Item -ItemType Directory -Force $layout | Out-Null
dotnet publish $proj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -o $layout
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失敗" }

# 2) マニフェストを版番号差し込みでレイアウト直下へ
(Get-Content $manifest -Raw -Encoding UTF8).Replace('$VERSION$', $pkgVersion) |
  Set-Content (Join-Path $layout "AppxManifest.xml") -Encoding UTF8

# 3) Assets をコピー
Copy-Item $assets (Join-Path $layout "Assets") -Recurse -Force

# 4) MakeAppx で pack（Windows SDK）
$makeappx = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
  Sort-Object FullName -Descending | Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
if (-not $makeappx) { throw "makeappx.exe が見つかりません（Windows SDK が必要）" }
& $makeappx.FullName pack /d $layout /p $outMsix /o
if ($LASTEXITCODE -ne 0) { throw "MakeAppx pack 失敗" }
Write-Host "MSIX 生成: $outMsix"

# 5) （任意）ローカル検証用の自己署名。Store 提出には不要。
if ($Sign) {
  # Publisher は manifest の CN と完全一致させる必要がある
  $publisher = "CN=42DACE4F-C681-4106-89D1-B5D746E48DE1"
  $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher `
    -KeyUsage DigitalSignature -FriendlyName "ShortcutBoard MSIX (local test)" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
  $signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
  if (-not $signtool) { throw "signtool.exe が見つかりません" }
  & $signtool.FullName sign /fd SHA256 /sha1 $cert.Thumbprint $outMsix
  Write-Host "自己署名しました（ローカル検証用）。インストールには証明書を信頼済みの場所へ追加する必要があります。"
  Write-Host "Store 提出にはこの署名は不要です（署名なし .msix を提出してください）。"
}

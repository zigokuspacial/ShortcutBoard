#!/bin/bash
# ShortcutBoard.app バンドルと .dmg を生成する（macOS ランナーで実行）。
# 使い方: make-bundle.sh <publish_dir> <out_dir> <arch_label>
#   publish_dir : dotnet publish の出力フォルダ（自己完結型）
#   out_dir     : .app / .dmg の出力先
#   arch_label  : 例) AppleSilicon / Intel
set -euo pipefail

PUBLISH_DIR="$1"
OUT_DIR="$2"
ARCH_LABEL="$3"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP="$OUT_DIR/ShortcutBoard.app"

echo "==> bundling $APP ($ARCH_LABEL) from $PUBLISH_DIR"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# Info.plist
cp "$SCRIPT_DIR/Info.plist" "$APP/Contents/Info.plist"

# 実行ファイル一式（自己完結型: .NET ランタイム不要）
cp -R "$PUBLISH_DIR"/* "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/ShortcutBoard"

# アイコン（PNG -> icns）。失敗してもバンドルは有効。
if [ -f "$SCRIPT_DIR/icon-1024.png" ]; then
  ICONSET="$(mktemp -d)/AppIcon.iconset"
  mkdir -p "$ICONSET"
  for s in 16 32 64 128 256 512; do
    sips -z $s $s        "$SCRIPT_DIR/icon-1024.png" --out "$ICONSET/icon_${s}x${s}.png"      >/dev/null 2>&1 || true
    sips -z $((s*2)) $((s*2)) "$SCRIPT_DIR/icon-1024.png" --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null 2>&1 || true
  done
  iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns" 2>/dev/null \
    || sips -s format icns "$SCRIPT_DIR/icon-1024.png" --out "$APP/Contents/Resources/AppIcon.icns" >/dev/null 2>&1 \
    || echo "WARN: icon generation skipped"
fi

# --- 署名・公証はここに後から追加できる ---
# 例: codesign --deep --force --options runtime --sign "Developer ID Application: NAME" "$APP"
#     xcrun notarytool submit "$DMG" --keychain-profile "AC_PASSWORD" --wait
#     xcrun stapler staple "$DMG"

# .dmg 生成
mkdir -p "$OUT_DIR"
DMG="$OUT_DIR/ShortcutBoard-$ARCH_LABEL.dmg"
rm -f "$DMG"
hdiutil create -volname "ShortcutBoard" -srcfolder "$APP" -ov -format UDZO "$DMG"

echo "==> created: $APP"
echo "==> created: $DMG"

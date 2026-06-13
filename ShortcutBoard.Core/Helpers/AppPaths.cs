using System.IO;
using System.Runtime.InteropServices;

namespace ShortcutBoard.Helpers;

/// <summary>
/// アプリが使用するファイルパスを一元管理する。（Windows/macOS 共通）
///  - Windows: %AppData%\ShortcutBoard
///  - macOS  : ~/Library/Application Support/ShortcutBoard
/// いずれもアプリ本体とは別の場所のため、アンインストールしてもデータは残る。
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// データ保存ルート。環境変数 SHORTCUTBOARD_DATA_DIR が設定されていればそれを優先する
    /// （自動テストでの隔離用。本番では未設定のため通常パスになる）。
    /// </summary>
    public static string RootDir => ResolveRootDir();

    public static string ShortcutsFile => Path.Combine(RootDir, "shortcuts.json");

    /// <summary>最終正常版バックアップ（保存成功のたびに更新）。</summary>
    public static string BackupFile => Path.Combine(RootDir, "shortcuts.backup.json");

    /// <summary>日次スナップショット（7世代）の保存先。</summary>
    public static string SnapshotsDir => Path.Combine(RootDir, "backups");

    public static string SettingsFile => Path.Combine(RootDir, "settings.json");

    public static string LogsDir => Path.Combine(RootDir, "logs");

    private static string ResolveRootDir()
    {
        var overrideDir = Environment.GetEnvironmentVariable("SHORTCUTBOARD_DATA_DIR");
        if (!string.IsNullOrEmpty(overrideDir))
            return overrideDir;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "ShortcutBoard");
        }

        // Windows（および既定）: %AppData%\ShortcutBoard（従来どおり）
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShortcutBoard");
    }

    /// <summary>必要なフォルダを作成する。</summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(LogsDir);
    }
}

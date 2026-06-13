using System.Diagnostics;
using System.IO;
using ShortcutBoard.Services;

namespace ShortcutBoard.Mac.Services;

/// <summary>
/// macOS のログイン時自動起動（LaunchAgent plist）を管理する。
/// ~/Library/LaunchAgents/com.shortcutboard.app.plist を作成/削除する。
/// </summary>
public class MacStartupService
{
    private const string Label = "com.shortcutboard.app";

    private readonly LogService _log = LogService.Instance;

    private static string PlistPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "LaunchAgents", $"{Label}.plist");
        }
    }

    public bool IsEnabled() => File.Exists(PlistPath);

    public void SetEnabled(bool enabled)
    {
        try
        {
            var dir = Path.GetDirectoryName(PlistPath)!;
            Directory.CreateDirectory(dir);

            if (enabled)
            {
                var exe = GetExecutablePath();
                var plist = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>{Label}</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{exe}</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                </dict>
                </plist>
                """;
                File.WriteAllText(PlistPath, plist);
                _log.Info($"自動起動を有効化: {PlistPath}");
            }
            else
            {
                if (File.Exists(PlistPath)) File.Delete(PlistPath);
                _log.Info("自動起動を無効化。");
            }
        }
        catch (Exception ex)
        {
            _log.Error("自動起動設定の変更に失敗。", ex);
        }
    }

    private static string GetExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path)) return path;
        return Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
    }
}

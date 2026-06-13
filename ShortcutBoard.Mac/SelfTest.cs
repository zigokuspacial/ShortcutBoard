using System.IO;
using ShortcutBoard.Models;
using ShortcutBoard.Services;

namespace ShortcutBoard.Mac;

/// <summary>
/// UI を起動せずに、設定・一覧の読み書きが成立するかを確認する自己診断。
/// CI（macOS ランナー）で `ShortcutBoard --selftest` として実行される。
/// </summary>
internal static class SelfTest
{
    public static int Run()
    {
        try
        {
            // 隔離した一時フォルダを使用（実ユーザーデータを汚さない）
            var dir = Path.Combine(Path.GetTempPath(), "sb-selftest-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("SHORTCUTBOARD_DATA_DIR", dir);

            var storage = new JsonStorageService();

            // 1) 初回ロードでサンプルが作られる
            var list = storage.LoadShortcuts();
            if (list.Count == 0)
            {
                Console.Error.WriteLine("[selftest] FAILED: サンプルデータが作成されない");
                return 1;
            }

            // 2) 一覧の保存→再読込（並び順保持）
            list.Add(new Shortcut { Keys = "Cmd + K", Name = "テスト", Category = "自作" });
            storage.SaveShortcuts(list);
            var reloaded = storage.LoadShortcuts();
            if (reloaded.Count != list.Count || reloaded[^1].Name != "テスト")
            {
                Console.Error.WriteLine("[selftest] FAILED: 一覧の保存/読込が一致しない");
                return 1;
            }

            // 3) 設定の保存→再読込
            var s = new AppSettings { HotkeyDisplay = "Ctrl + Shift + A", RunAtStartup = true, HasShownWelcome = true };
            storage.SaveSettings(s);
            var rs = storage.LoadSettings();
            if (rs.HotkeyDisplay != "Ctrl + Shift + A" || !rs.RunAtStartup || !rs.HasShownWelcome)
            {
                Console.Error.WriteLine("[selftest] FAILED: 設定の保存/読込が一致しない");
                return 1;
            }

            Directory.Delete(dir, recursive: true);
            Console.WriteLine("[selftest] OK: 設定・一覧の読み書きに成功しました");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[selftest] FAILED with exception: " + ex);
            return 1;
        }
    }
}

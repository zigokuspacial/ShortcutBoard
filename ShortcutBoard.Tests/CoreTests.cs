using System.IO;
using System.Text;
using ShortcutBoard.Data;
using ShortcutBoard.Helpers;
using ShortcutBoard.Models;
using ShortcutBoard.Services;
using Xunit;

namespace ShortcutBoard.Tests;

/// <summary>
/// 共通ロジック（ShortcutBoard.Core）の自動テスト。
/// 1クラスにまとめて直列実行し、SHORTCUTBOARD_DATA_DIR を一時フォルダへ向けて隔離する。
/// Windows / macOS 双方の GitHub Actions ランナーで実行される。
/// </summary>
public class CoreTests : IDisposable
{
    private readonly string _dir;

    public CoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "sb-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable("SHORTCUTBOARD_DATA_DIR", _dir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SHORTCUTBOARD_DATA_DIR", null);
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void AppPaths_UsesOverrideDir()
    {
        Assert.Equal(_dir, AppPaths.RootDir);
        Assert.Equal(Path.Combine(_dir, "shortcuts.json"), AppPaths.ShortcutsFile);
    }

    [Fact]
    public void FirstLoad_CreatesSampleData()
    {
        var storage = new JsonStorageService();
        var list = storage.LoadShortcuts();

        Assert.NotEmpty(list);
        Assert.Equal(SampleData.Create().Count, list.Count);
        Assert.True(File.Exists(AppPaths.ShortcutsFile));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_AndPreservesOrder()
    {
        var storage = new JsonStorageService();
        var items = new List<Shortcut>
        {
            new() { Keys = "Cmd + 1", Name = "First",  Category = "A" },
            new() { Keys = "Cmd + 2", Name = "Second", Category = "B" },
            new() { Keys = "Cmd + 3", Name = "Third",  Category = "A" },
        };
        storage.SaveShortcuts(items);

        var loaded = storage.LoadShortcuts();

        Assert.Equal(3, loaded.Count);
        // 並び順が保持される
        Assert.Equal("First", loaded[0].Name);
        Assert.Equal("Second", loaded[1].Name);
        Assert.Equal("Third", loaded[2].Name);
        // 値が保持される
        Assert.Equal("Cmd + 2", loaded[1].Keys);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var storage = new JsonStorageService();
        var s = new AppSettings
        {
            HotkeyDisplay = "Ctrl + Shift + A",
            RunAtStartup = true,
            HasShownWelcome = true,
            HotkeyModifiers = 6,
            HotkeyVirtualKey = 65,
        };
        storage.SaveSettings(s);

        var loaded = storage.LoadSettings();
        Assert.Equal("Ctrl + Shift + A", loaded.HotkeyDisplay);
        Assert.True(loaded.RunAtStartup);
        Assert.True(loaded.HasShownWelcome);
        Assert.Equal(6u, loaded.HotkeyModifiers);
        Assert.Equal(65u, loaded.HotkeyVirtualKey);
    }

    [Fact]
    public void CorruptedJson_NoBackup_IsBackedUp_AndRecoversWithSamples()
    {
        AppPaths.EnsureDirectories();
        File.WriteAllText(AppPaths.ShortcutsFile, "{ this is not valid json ]", Encoding.UTF8);

        var storage = new JsonStorageService();
        var list = storage.LoadShortcuts();

        // バックアップが無い場合のみサンプルで復旧する
        Assert.NotEmpty(list);
        Assert.True(storage.LastLoadUsedFallback);
        // 破損ファイルの退避(.bak)が作られる
        var backups = Directory.GetFiles(_dir, "shortcuts.json.corrupted-*.bak");
        Assert.NotEmpty(backups);
    }

    [Fact]
    public void MissingMainFile_WithBackup_RestoresUserData_NotSamples()
    {
        // ユーザーデータを保存（backup も作られる）→ メインだけ消失、を再現
        var storage = new JsonStorageService();
        storage.SaveShortcuts(new List<Shortcut>
        {
            new() { Keys = "Ctrl + Shift + S", Name = "ユーザーの大事なデータ", Category = "自作" },
        });
        File.Delete(AppPaths.ShortcutsFile);

        var loaded = new JsonStorageService().LoadShortcuts();

        // サンプルではなくバックアップから復元される
        Assert.Single(loaded);
        Assert.Equal("ユーザーの大事なデータ", loaded[0].Name);
        // メインファイルも書き戻されている
        Assert.True(File.Exists(AppPaths.ShortcutsFile));
    }

    [Fact]
    public void CorruptedMainFile_WithBackup_RestoresUserData_NotSamples()
    {
        var storage = new JsonStorageService();
        storage.SaveShortcuts(new List<Shortcut>
        {
            new() { Keys = "Ctrl + 1", Name = "復元されるべきデータ", Category = "A" },
        });
        File.WriteAllText(AppPaths.ShortcutsFile, "BROKEN!!", Encoding.UTF8);

        var loaded = new JsonStorageService().LoadShortcuts();

        Assert.Single(loaded);
        Assert.Equal("復元されるべきデータ", loaded[0].Name);
        // 破損ファイルは退避されている
        Assert.NotEmpty(Directory.GetFiles(_dir, "shortcuts.json.corrupted-*.bak"));
    }

    [Fact]
    public void SuccessfulLoad_CreatesDailySnapshot()
    {
        var storage = new JsonStorageService();
        storage.SaveShortcuts(new List<Shortcut> { new() { Keys = "K", Name = "N" } });
        storage.LoadShortcuts(); // 読み込み成功 → スナップショット作成

        var snaps = Directory.GetFiles(Path.Combine(_dir, "backups"), "shortcuts-*.json");
        Assert.NotEmpty(snaps);
    }

    [Fact]
    public void NotFirstRun_NothingReadable_StartsEmpty_AndWritesNothing()
    {
        // settings.json を先に作る＝初回起動ではない状態
        var s0 = new JsonStorageService();
        s0.SaveSettings(new AppSettings());

        // データファイルは一切無い（ログオン時の一時異常を再現）
        var storage = new JsonStorageService();
        var list = storage.LoadShortcuts();

        Assert.Empty(list);                       // サンプルではなく空で起動
        Assert.True(storage.LastLoadUsedFallback);
        Assert.False(File.Exists(AppPaths.ShortcutsFile));  // 何も書き込まない
        Assert.False(File.Exists(AppPaths.BackupFile));
    }

    [Fact]
    public void SampleMain_WithRealBackup_RestoresRealData()
    {
        // 実データを保存（backup作成）→ メインがサンプルに置き換わった事故を再現
        var storage = new JsonStorageService();
        storage.SaveShortcuts(new List<Shortcut>
        {
            new() { Keys = "Ctrl + Shift + S", Name = "Chrome 全画面キャプチャ", Category = "自作" },
        });
        var sampleJson = System.Text.Json.JsonSerializer.Serialize(SampleData.Create());
        File.WriteAllText(AppPaths.ShortcutsFile, sampleJson, Encoding.UTF8);

        var loaded = new JsonStorageService().LoadShortcuts();

        // サンプルを「正常データ」と誤認せず、バックアップの実データを復元する
        Assert.Single(loaded);
        Assert.Equal("Chrome 全画面キャプチャ", loaded[0].Name);
        // サンプルだったメインは退避されている
        Assert.NotEmpty(Directory.GetFiles(_dir, "shortcuts.json.sample-replaced-*.bak"));
    }

    [Fact]
    public void SavingSamples_DoesNotOverwriteRealBackup()
    {
        var storage = new JsonStorageService();
        storage.SaveShortcuts(new List<Shortcut>
        {
            new() { Keys = "Ctrl + 9", Name = "実データ", Category = "自作" },
        });

        // サンプルそのものを保存してもバックアップは実データのまま
        storage.SaveShortcuts(SampleData.Create());

        var backup = System.Text.Json.JsonSerializer.Deserialize<List<Shortcut>>(
            File.ReadAllText(AppPaths.BackupFile));
        Assert.Single(backup!);
        Assert.Equal("実データ", backup![0].Name);
    }

    [Fact]
    public void IsSample_DetectsSampleSet_ButNotUserData()
    {
        Assert.True(SampleData.IsSample(SampleData.Create()));
        Assert.True(SampleData.IsSample(SampleData.CreateWindows()));
        Assert.True(SampleData.IsSample(SampleData.CreateMac()));

        var user = SampleData.CreateWindows();
        user.RemoveAt(0);
        user.Add(new Shortcut { Keys = "Ctrl を2回", Name = "Clibor起動" });
        Assert.False(SampleData.IsSample(user));
    }

    [Fact]
    public void WindowsSample_Is10ExpectedItems()
    {
        var s = SampleData.CreateWindows();

        Assert.Equal(10, s.Count);

        // 仕様どおりの項目（キー / 名前 / カテゴリ）
        var expected = new (string Keys, string Name, string Category)[]
        {
            ("Ctrl + Shift + A", "この一覧を表示",           "ShortcutBoard"),
            ("Alt + Tab",        "アプリを切り替える",        "ウィンドウ"),
            ("Win + Tab",        "タスクビュー",             "ウィンドウ"),
            ("Win + D",          "デスクトップを表示",        "ウィンドウ"),
            ("Win + ← / →",      "ウィンドウを左右に並べる",  "ウィンドウ"),
            ("Win + Shift + S",  "範囲を切り取り",           "スクリーンショット"),
            ("Win + V",          "クリップボード履歴",        "入力"),
            ("Win + .",          "絵文字・記号を入力",        "入力"),
            ("Win + L",          "画面をロック",             "システム"),
            ("Win + E",          "エクスプローラーを開く",    "ファイル"),
        };
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Keys, s[i].Keys);
            Assert.Equal(expected[i].Name, s[i].Name);
            Assert.Equal(expected[i].Category, s[i].Category);
            Assert.False(string.IsNullOrWhiteSpace(s[i].Description));
        }
    }

    [Fact]
    public void WindowsSample_ExcludesEnvironmentSpecificItems()
    {
        var all = string.Join("\n", SampleData.CreateWindows()
            .Select(s => $"{s.Name} {s.Keys} {s.Category} {s.Description}"));

        // 特定環境/ユーザー固有の項目は初期サンプルに含めない
        Assert.DoesNotContain("PowerToys", all);
        Assert.DoesNotContain("ChatGPT", all);
        Assert.DoesNotContain("Claude", all);
    }

    [Fact]
    public void MacSample_Is10Items_AndExcludesWinKey()
    {
        var s = SampleData.CreateMac();
        Assert.Equal(10, s.Count);
        Assert.All(s, x => Assert.False(string.IsNullOrWhiteSpace(x.Description)));
        // Mac サンプルに Windows 専用の "Win +" は出てこない
        Assert.DoesNotContain("Win +", string.Join("\n", s.Select(x => x.Keys)));
    }

    [Fact]
    public void Delete_PersistsRemoval_OnlyTarget_AndUndoRestoresPosition()
    {
        var storage = new JsonStorageService();
        var list = new List<Shortcut>
        {
            new() { Keys = "A", Name = "一", Category = "x" },
            new() { Keys = "B", Name = "二", Category = "x" },
            new() { Keys = "C", Name = "三", Category = "x" },
        };
        storage.SaveShortcuts(list);

        // 真ん中(index1)を削除して保存 → 再読込
        var removed = list[1];
        list.RemoveAt(1);
        storage.SaveShortcuts(list);
        var afterDelete = storage.LoadShortcuts();

        Assert.Equal(2, afterDelete.Count);
        Assert.DoesNotContain(afterDelete, x => x.Name == "二");  // 対象は消える
        Assert.Contains(afterDelete, x => x.Name == "一");        // 他は消えない
        Assert.Contains(afterDelete, x => x.Name == "三");

        // 元に戻す: 元の位置(index1)へ挿入して保存 → 再読込
        list.Insert(1, removed);
        storage.SaveShortcuts(list);
        var afterUndo = storage.LoadShortcuts();

        Assert.Equal(3, afterUndo.Count);
        Assert.Equal("二", afterUndo[1].Name);   // 元の位置に戻る
    }

    [Fact]
    public void Save_UpdatesLastKnownGoodBackup()
    {
        var storage = new JsonStorageService();
        storage.SaveShortcuts(new List<Shortcut> { new() { Keys = "K1", Name = "v1" } });
        storage.SaveShortcuts(new List<Shortcut> { new() { Keys = "K2", Name = "v2" } });

        Assert.True(File.Exists(AppPaths.BackupFile));
        var backupJson = File.ReadAllText(AppPaths.BackupFile);
        Assert.Contains("v2", backupJson); // 最終正常版が反映されている
    }

    [Fact]
    public void Search_MatchesByKeywordAcrossFields()
    {
        var s = new Shortcut { Keys = "Ctrl + Alt + C", Name = "Claudeを開く", Description = "AIアシスタント", Category = "Claude" };

        Assert.True(ShortcutSearch.Matches(s, "claude", ShortcutSearch.AllCategories)); // 大小無視
        Assert.True(ShortcutSearch.Matches(s, "Alt", ShortcutSearch.AllCategories));    // キー
        Assert.True(ShortcutSearch.Matches(s, "アシスタント", ShortcutSearch.AllCategories)); // 説明
        Assert.False(ShortcutSearch.Matches(s, "存在しない", ShortcutSearch.AllCategories));
    }

    [Fact]
    public void Search_FiltersByCategory()
    {
        var s = new Shortcut { Keys = "K", Name = "N", Category = "Windows" };

        Assert.True(ShortcutSearch.Matches(s, null, "Windows"));
        Assert.False(ShortcutSearch.Matches(s, null, "Chrome"));
        Assert.True(ShortcutSearch.Matches(s, null, ShortcutSearch.AllCategories));
    }

    [Fact]
    public void BuildCategoryList_StartsWithAll_AndIsDistinct()
    {
        var items = new List<Shortcut>
        {
            new() { Category = "Windows" },
            new() { Category = "Chrome" },
            new() { Category = "Windows" },
            new() { Category = "" }, // 未分類になる
        };

        var cats = ShortcutSearch.BuildCategoryList(items);

        Assert.Equal(ShortcutSearch.AllCategories, cats[0]);
        Assert.Contains("Windows", cats);
        Assert.Contains("Chrome", cats);
        Assert.Contains("未分類", cats);
        // Windows は1回だけ
        Assert.Equal(1, cats.Count(c => c == "Windows"));
    }
}

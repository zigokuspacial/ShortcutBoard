using System.Runtime.InteropServices;
using ShortcutBoard.Models;

namespace ShortcutBoard.Data;

/// <summary>
/// 初回起動時・復旧時に投入するサンプルデータ。
/// OS を判定して Windows / macOS 向けの内容を出し分ける。
/// いずれも「押したら実際に動く」標準ショートカットのみ（特定アプリ依存・自作前提は含めない）。
/// </summary>
public static class SampleData
{
    /// <summary>現在のOSに適したサンプルを返す。</summary>
    public static List<Shortcut> Create()
        => RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? CreateMac() : CreateWindows();

    /// <summary>Windows 向け初期サンプル（標準で動作する10件）。</summary>
    public static List<Shortcut> CreateWindows() => new()
    {
        new Shortcut { Keys = "Ctrl + Shift + A", Name = "この一覧を表示",            Description = "いつでもショートカット一覧を呼び出します（設定で変更可）。", Category = "ShortcutBoard" },
        new Shortcut { Keys = "Alt + Tab",        Name = "アプリを切り替える",        Description = "開いているアプリを順に切り替えます。",                   Category = "ウィンドウ" },
        new Shortcut { Keys = "Win + Tab",        Name = "タスクビュー",              Description = "開いているウィンドウを一覧表示します。",                 Category = "ウィンドウ" },
        new Shortcut { Keys = "Win + D",          Name = "デスクトップを表示",        Description = "すべてのウィンドウを最小化／元に戻します。",             Category = "ウィンドウ" },
        new Shortcut { Keys = "Win + ← / →",      Name = "ウィンドウを左右に並べる",  Description = "ウィンドウを画面の左右半分にスナップします。",           Category = "ウィンドウ" },
        new Shortcut { Keys = "Win + Shift + S",  Name = "範囲を切り取り",            Description = "画面の好きな範囲を選んでスクリーンショットします。",     Category = "スクリーンショット" },
        new Shortcut { Keys = "Win + V",          Name = "クリップボード履歴",        Description = "過去にコピーした内容から貼り付けます。",                 Category = "入力" },
        new Shortcut { Keys = "Win + .",          Name = "絵文字・記号を入力",        Description = "絵文字／記号パネルを開きます。",                         Category = "入力" },
        new Shortcut { Keys = "Win + L",          Name = "画面をロック",              Description = "離席時に画面をロックします。",                           Category = "システム" },
        new Shortcut { Keys = "Win + E",          Name = "エクスプローラーを開く",    Description = "ファイル／フォルダーの管理画面を開きます。",             Category = "ファイル" },
    };

    /// <summary>macOS 向け初期サンプル（標準で動作する10件）。</summary>
    public static List<Shortcut> CreateMac() => new()
    {
        new Shortcut { Keys = "Ctrl + Shift + A",  Name = "この一覧を表示",          Description = "いつでもショートカット一覧を呼び出します（設定で変更可）。", Category = "ShortcutBoard" },
        new Shortcut { Keys = "Cmd + Tab",         Name = "アプリを切り替える",      Description = "開いているアプリを順に切り替えます。",                   Category = "ウィンドウ" },
        new Shortcut { Keys = "Ctrl + ↑",          Name = "Mission Control",         Description = "開いているウィンドウを一覧表示します。",                 Category = "ウィンドウ" },
        new Shortcut { Keys = "Cmd + H",           Name = "ウィンドウを隠す",        Description = "最前面のアプリを隠します。",                             Category = "ウィンドウ" },
        new Shortcut { Keys = "Cmd + Space",       Name = "Spotlight 検索",          Description = "アプリやファイルを検索して起動します。",                 Category = "システム" },
        new Shortcut { Keys = "Cmd + Shift + 4",   Name = "範囲を切り取り",          Description = "画面の好きな範囲を選んでスクリーンショットします。",     Category = "スクリーンショット" },
        new Shortcut { Keys = "Cmd + Shift + 5",   Name = "スクショ・収録メニュー",  Description = "スクリーンショットと画面収録のメニューを開きます。",     Category = "スクリーンショット" },
        new Shortcut { Keys = "Ctrl + Cmd + Space",Name = "絵文字・記号を入力",      Description = "絵文字／記号ビューアを開きます。",                       Category = "入力" },
        new Shortcut { Keys = "Ctrl + Cmd + Q",    Name = "画面をロック",            Description = "離席時に画面をロックします。",                           Category = "システム" },
        new Shortcut { Keys = "Cmd + Space → Finder", Name = "Finder を開く",        Description = "ファイル／フォルダーの管理画面を開きます。",             Category = "ファイル" },
    };

    /// <summary>
    /// 一覧が「サンプルデータそのもの」かどうかを判定する。
    /// ユーザーデータをサンプルと誤認してバックアップ等を壊さないための判定に使う。
    /// Windows/macOS いずれのサンプル定義とも照合する（OS をまたいでも誤判定しないように）。
    /// </summary>
    public static bool IsSample(IReadOnlyCollection<Shortcut> items)
        => MatchesSampleSet(items, CreateWindows()) || MatchesSampleSet(items, CreateMac());

    private static bool MatchesSampleSet(IReadOnlyCollection<Shortcut> items, List<Shortcut> sample)
    {
        if (items.Count != sample.Count) return false;
        var a = items.Select(s => (s.Keys, s.Name)).OrderBy(x => x.Keys).ThenBy(x => x.Name);
        var b = sample.Select(s => (s.Keys, s.Name)).OrderBy(x => x.Keys).ThenBy(x => x.Name);
        return a.SequenceEqual(b);
    }
}

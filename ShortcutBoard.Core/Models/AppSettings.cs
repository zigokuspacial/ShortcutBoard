namespace ShortcutBoard.Models;

/// <summary>
/// アプリ設定。settings.json に保存される。（Windows/macOS 共通）
/// ホットキーの数値表現はプラットフォームごとに解釈する（HotkeyDisplay は共通の表示用）。
/// </summary>
public class AppSettings
{
    /// <summary>ホットキーの修飾キー（プラットフォーム依存のビット値）。</summary>
    public uint HotkeyModifiers { get; set; } = HotkeyDefaults.DefaultModifiers;

    /// <summary>ホットキーの仮想キーコード（プラットフォーム依存）。</summary>
    public uint HotkeyVirtualKey { get; set; } = HotkeyDefaults.DefaultVk;

    /// <summary>表示用のホットキー文字列（例: "Ctrl + Shift + A"）。</summary>
    public string HotkeyDisplay { get; set; } = "Ctrl + Shift + A";

    /// <summary>OS 起動/ログイン時に自動起動するか。</summary>
    public bool RunAtStartup { get; set; } = false;

    /// <summary>初回起動時の案内を表示済みか。</summary>
    public bool HasShownWelcome { get; set; } = false;
}

/// <summary>ホットキー既定値（Windows の Win32 修飾フラグ基準）。</summary>
public static class HotkeyDefaults
{
    // MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    // VK_SPACE = 0x20, A = 0x41
    public const uint VK_SPACE = 0x20;
    public const uint VK_A = 0x41;

    /// <summary>既定: Ctrl + Shift + A</summary>
    public const uint DefaultModifiers = MOD_CONTROL | MOD_SHIFT;
    public const uint DefaultVk = VK_A;
}

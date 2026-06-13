using System.Windows.Input;
using ShortcutBoard.Models;

namespace ShortcutBoard.Helpers;

/// <summary>
/// WPF の Key/ModifierKeys と Win32 の修飾フラグ・仮想キーコードを相互変換する。
/// </summary>
public static class HotkeyParser
{
    /// <summary>
    /// WPF のキー入力から Win32 用の修飾フラグ・VK・表示文字列を作成する。
    /// 修飾キーのみ、または無効なキーの場合は false。
    /// </summary>
    public static bool TryBuild(ModifierKeys modifiers, Key key,
        out uint win32Modifiers, out uint vk, out string display)
    {
        win32Modifiers = 0;
        vk = 0;
        display = string.Empty;

        // 修飾キー単体は無効
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.System or Key.None)
        {
            return false;
        }

        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) { win32Modifiers |= HotkeyDefaults.MOD_CONTROL; parts.Add("Ctrl"); }
        if (modifiers.HasFlag(ModifierKeys.Alt))     { win32Modifiers |= HotkeyDefaults.MOD_ALT; parts.Add("Alt"); }
        if (modifiers.HasFlag(ModifierKeys.Shift))   { win32Modifiers |= HotkeyDefaults.MOD_SHIFT; parts.Add("Shift"); }
        if (modifiers.HasFlag(ModifierKeys.Windows)) { win32Modifiers |= HotkeyDefaults.MOD_WIN; parts.Add("Win"); }

        // 最低1つの修飾キーを必須にする（誤爆防止）
        if (win32Modifiers == 0)
            return false;

        vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        parts.Add(KeyToDisplay(key));
        display = string.Join(" + ", parts);
        return true;
    }

    /// <summary>修飾キーのみを表示用文字列にする（キャプチャ中のプレビュー用）。</summary>
    public static string FormatModifiersOnly(ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        return string.Join(" + ", parts);
    }

    /// <summary>
    /// 押されたキーの組み合わせを表示用文字列にする（修飾キー無しも許可）。
    /// 一覧のキー入力欄でのキャプチャに使用。
    /// </summary>
    public static string FormatKeyCombo(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(KeyToDisplay(key));
        return string.Join(" + ", parts);
    }

    private static string KeyToDisplay(Key key) => key switch
    {
        Key.Space => "Space",
        Key.Return => "Enter",
        Key.Tab => "Tab",
        Key.Back => "Backspace",
        Key.Delete => "Delete",
        Key.Escape => "Esc",
        Key.Left => "←",
        Key.Right => "→",
        Key.Up => "↑",
        Key.Down => "↓",
        Key.Next => "PageDown",
        Key.Prior => "PageUp",
        Key.Home => "Home",
        Key.End => "End",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemQuestion => "/",
        Key.OemMinus => "-",
        Key.OemPlus => "+",
        Key.Oem1 => ";",
        Key.Oem3 => "@",
        Key.Oem4 => "[",
        Key.Oem6 => "]",
        Key.Oem5 => "\\",
        Key.Oem7 => "^",
        >= Key.D0 and <= Key.D9 => key.ToString().Substring(1),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num" + key.ToString().Substring(6),
        _ => key.ToString(),
    };
}

using Avalonia.Input;

namespace ShortcutBoard.Mac.Helpers;

/// <summary>
/// Avalonia のキー入力と、表示文字列／Carbon ホットキーコードの相互変換。
/// </summary>
public static class MacKeyParser
{
    // Carbon の修飾フラグ
    public const uint CmdKey = 0x0100;
    public const uint ShiftKey = 0x0200;
    public const uint OptionKey = 0x0800;
    public const uint ControlKey = 0x1000;

    /// <summary>押された組み合わせを表示用文字列にする（修飾なしも許可）。</summary>
    public static string FormatKeyCombo(KeyModifiers mods, Key key)
    {
        var parts = new List<string>();
        if (mods.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (mods.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (mods.HasFlag(KeyModifiers.Meta)) parts.Add("Cmd");
        parts.Add(KeyToDisplay(key));
        return string.Join(" + ", parts);
    }

    public static string FormatModifiersOnly(KeyModifiers mods)
    {
        var parts = new List<string>();
        if (mods.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (mods.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (mods.HasFlag(KeyModifiers.Meta)) parts.Add("Cmd");
        return string.Join(" + ", parts);
    }

    public static bool IsModifier(Key k) =>
        k is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.System or Key.None;

    /// <summary>
    /// 表示文字列（例 "Ctrl + Shift + A"）を Carbon の修飾マスク・キーコードへ変換する。
    /// グローバルホットキー登録に使用。
    /// </summary>
    public static bool TryParseDisplay(string display, out uint carbonModifiers, out uint carbonKeyCode)
    {
        carbonModifiers = 0;
        carbonKeyCode = 0;
        if (string.IsNullOrWhiteSpace(display)) return false;

        var tokens = display.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? keyToken = null;

        foreach (var t in tokens)
        {
            switch (t.ToLowerInvariant())
            {
                case "ctrl": case "control": carbonModifiers |= ControlKey; break;
                case "alt": case "option": case "opt": carbonModifiers |= OptionKey; break;
                case "shift": carbonModifiers |= ShiftKey; break;
                case "cmd": case "command": case "⌘": carbonModifiers |= CmdKey; break;
                default: keyToken = t; break;
            }
        }

        if (keyToken is null) return false;
        return TryKeyTokenToCarbon(keyToken, out carbonKeyCode);
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
        >= Key.D0 and <= Key.D9 => key.ToString().Substring(1),
        _ => key.ToString(),
    };

    // 表示トークン → Carbon 仮想キーコード（よく使う範囲を網羅）
    private static bool TryKeyTokenToCarbon(string token, out uint code)
    {
        code = 0;
        token = token.Trim();

        var letters = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["A"]=0x00,["S"]=0x01,["D"]=0x02,["F"]=0x03,["H"]=0x04,["G"]=0x05,["Z"]=0x06,
            ["X"]=0x07,["C"]=0x08,["V"]=0x09,["B"]=0x0B,["Q"]=0x0C,["W"]=0x0D,["E"]=0x0E,
            ["R"]=0x0F,["Y"]=0x10,["T"]=0x11,["O"]=0x1F,["U"]=0x20,["I"]=0x22,["P"]=0x23,
            ["L"]=0x25,["J"]=0x26,["K"]=0x28,["N"]=0x2D,["M"]=0x2E,
            ["1"]=0x12,["2"]=0x13,["3"]=0x14,["4"]=0x15,["6"]=0x16,["5"]=0x17,
            ["9"]=0x19,["7"]=0x1A,["8"]=0x1C,["0"]=0x1D,
            ["Space"]=0x31,["Enter"]=0x24,["Tab"]=0x30,["Esc"]=0x35,
            ["←"]=0x7B,["→"]=0x7C,["↓"]=0x7D,["↑"]=0x7E,
            ["F1"]=0x7A,["F2"]=0x78,["F3"]=0x63,["F4"]=0x76,["F5"]=0x60,["F6"]=0x61,
            ["F7"]=0x62,["F8"]=0x64,["F9"]=0x65,["F10"]=0x6D,["F11"]=0x67,["F12"]=0x6F,
        };

        return letters.TryGetValue(token, out code);
    }
}

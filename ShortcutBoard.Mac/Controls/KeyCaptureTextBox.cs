using Avalonia.Controls;
using Avalonia.Input;
using ShortcutBoard.Mac.Helpers;

namespace ShortcutBoard.Mac.Controls;

/// <summary>
/// キー入力欄。直接文字入力もできるが、Ctrl/Alt/Cmd を押しながらキーを押すと
/// その組み合わせを自動でキャプチャして文字に変換する。（Windows 版と同等）
/// Text を双方向バインドして Shortcut.Keys に反映する。
/// </summary>
public class KeyCaptureTextBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var mods = e.KeyModifiers;
        bool chord = mods.HasFlag(KeyModifiers.Control)
                     || mods.HasFlag(KeyModifiers.Alt)
                     || mods.HasFlag(KeyModifiers.Meta);

        if (!chord)
        {
            base.OnKeyDown(e); // 通常の文字入力
            return;
        }

        if (MacKeyParser.IsModifier(e.Key))
        {
            base.OnKeyDown(e);
            return;
        }

        // 修飾キー＋キー を確定
        Text = MacKeyParser.FormatKeyCombo(mods, e.Key);
        CaretIndex = Text?.Length ?? 0;
        e.Handled = true;
    }
}

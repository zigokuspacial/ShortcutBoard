using System.Windows.Controls;
using System.Windows.Input;
using ShortcutBoard.Helpers;

namespace ShortcutBoard.Controls;

/// <summary>
/// キー入力欄。2通りの入力に対応する。
///  - 直接文字入力（例: "Win + Tab" や "Ctrl を2回" など自由入力）
///  - Ctrl / Alt / Win を押しながらキーを押すと、その組み合わせを自動でキャプチャして文字に変換
/// Text を双方向バインドして Shortcut.Keys に反映する（通常の TextBox.Text バインドでOK）。
/// </summary>
public class KeyCaptureTextBox : TextBox
{
    public KeyCaptureTextBox()
    {
        // 直接タイプもできるよう編集可能にしておく
        IsReadOnly = false;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ctrl / Alt / Win のいずれかを押している場合のみ「ショートカット入力」とみなしてキャプチャ。
        // （Shift 単体は大文字入力等に使うのでキャプチャ対象にしない）
        bool chord = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
                     || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)
                     || Keyboard.Modifiers.HasFlag(ModifierKeys.Windows);

        if (!chord)
            return; // 通常の文字入力はそのまま通す

        // 修飾キー単体の段階では確定しない
        if (IsModifier(key))
            return;

        // 修飾キー＋キー を確定してテキスト化（バインド経由で Keys に入る）
        var combo = HotkeyParser.FormatKeyCombo(Keyboard.Modifiers, key);
        SetCurrentValue(TextProperty, combo);
        CaretIndex = Text.Length;
        e.Handled = true; // 文字としての入力は抑止
    }

    private static bool IsModifier(Key k) =>
        k is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System;
}

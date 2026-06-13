using System.Windows;
using System.Windows.Input;
using ShortcutBoard.Helpers;
using ShortcutBoard.ViewModels;

namespace ShortcutBoard.Views;

/// <summary>
/// 設定画面。ホットキー変更と Windows 自動起動の切り替えを行う。
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // IME/システムキーは実キーへ変換
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (HotkeyParser.TryBuild(Keyboard.Modifiers, key,
                out uint mod, out uint vk, out string display))
        {
            _vm.SetPendingHotkey(mod, vk, display);
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

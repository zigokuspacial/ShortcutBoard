using ShortcutBoard.Models;
using ShortcutBoard.ViewModels;

namespace ShortcutBoard.Mac.ViewModels;

/// <summary>macOS 版設定の ViewModel（ホットキー変更・ログイン時自動起動）。</summary>
public class MacSettingsViewModel : ObservableObject
{
    private string _hotkeyDisplay;
    public string HotkeyDisplay
    {
        get => _hotkeyDisplay;
        set => SetProperty(ref _hotkeyDisplay, value);
    }

    private bool _runAtStartup;
    public bool RunAtStartup
    {
        get => _runAtStartup;
        set => SetProperty(ref _runAtStartup, value);
    }

    public MacSettingsViewModel(AppSettings settings)
    {
        _hotkeyDisplay = settings.HotkeyDisplay;
        _runAtStartup = settings.RunAtStartup;
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.HotkeyDisplay = HotkeyDisplay;
        settings.RunAtStartup = RunAtStartup;
    }
}

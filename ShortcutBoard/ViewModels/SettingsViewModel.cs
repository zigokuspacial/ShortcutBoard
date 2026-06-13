using ShortcutBoard.Models;
using ShortcutBoard.Services;

namespace ShortcutBoard.ViewModels;

/// <summary>
/// 設定画面の ViewModel。ホットキー変更と Windows 自動起動を扱う。
/// </summary>
public class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    private uint _pendingModifiers;
    private uint _pendingVk;

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

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        _pendingModifiers = settings.HotkeyModifiers;
        _pendingVk = settings.HotkeyVirtualKey;
        _hotkeyDisplay = settings.HotkeyDisplay;
        _runAtStartup = settings.RunAtStartup;
    }

    /// <summary>キャプチャしたホットキーを反映する（まだ保存はしない）。</summary>
    public void SetPendingHotkey(uint modifiers, uint vk, string display)
    {
        _pendingModifiers = modifiers;
        _pendingVk = vk;
        HotkeyDisplay = display;
    }

    public uint PendingModifiers => _pendingModifiers;
    public uint PendingVk => _pendingVk;

    /// <summary>編集結果を AppSettings へ反映する（保存・登録は呼び出し側）。</summary>
    public void ApplyTo(AppSettings settings)
    {
        settings.HotkeyModifiers = _pendingModifiers;
        settings.HotkeyVirtualKey = _pendingVk;
        settings.HotkeyDisplay = HotkeyDisplay;
        settings.RunAtStartup = RunAtStartup;
    }
}

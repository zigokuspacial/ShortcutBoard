using System.Windows.Interop;
using ShortcutBoard.Helpers;

namespace ShortcutBoard.Services;

/// <summary>
/// グローバルホットキー（RegisterHotKey）を管理するサービス。
/// メイン表示用ウィンドウとは独立した、メッセージ受信専用の隠しウィンドウを使う。
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xB001;

    private readonly LogService _log = LogService.Instance;
    private HwndSource? _source;
    private bool _registered;

    /// <summary>ホットキーが押されたときに発火。</summary>
    public event Action? HotkeyPressed;

    /// <summary>メッセージ受信用の隠しウィンドウを生成する。アプリ起動時に1度呼ぶ。</summary>
    public void Initialize()
    {
        // 非表示の通常ウィンドウ（0x0、表示しない）。
        // ※メッセージ専用ウィンドウ(HWND_MESSAGE)では RegisterHotKey が機能しないため使わない。
        var parameters = new HwndSourceParameters("ShortcutBoardHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            PositionX = -10000,
            PositionY = -10000,
            WindowStyle = unchecked((int)0x80000000), // WS_POPUP（表示しない）
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>
    /// ホットキーを登録する。既存があれば一度解除してから登録。
    /// </summary>
    /// <returns>成功した場合 true。競合などで失敗した場合 false。</returns>
    public bool Register(uint modifiers, uint vk)
    {
        if (_source is null)
            throw new InvalidOperationException("Initialize() を先に呼んでください。");

        Unregister();

        // MOD_NOREPEAT で押しっぱなしの連続発火を防ぐ
        var ok = NativeMethods.RegisterHotKey(
            _source.Handle, HotkeyId, modifiers | NativeMethods.MOD_NOREPEAT, vk);

        _registered = ok;
        if (ok)
        {
            _log.Info($"ホットキー登録に成功 (mod=0x{modifiers:X}, vk=0x{vk:X})。");
        }
        else
        {
            var err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            _log.Warn($"ホットキー登録に失敗 (mod=0x{modifiers:X}, vk=0x{vk:X}, Win32Error={err})。競合の可能性。");
        }
        return ok;
    }

    public void Unregister()
    {
        if (_source is not null && _registered)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            try
            {
                HotkeyPressed?.Invoke();
            }
            catch (Exception ex)
            {
                _log.Error("ホットキー処理中に例外。", ex);
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
    }
}

using System.Runtime.InteropServices;
using ShortcutBoard.Mac.Helpers;
using ShortcutBoard.Services;

namespace ShortcutBoard.Mac.Services;

/// <summary>
/// macOS のグローバルホットキー（Carbon RegisterEventHotKey）を管理する。
/// 表示文字列（例 "Ctrl + Shift + A"）を解釈して登録する。
/// ※実機(macOS)での動作確認が必要。失敗時はログのみ（トレイメニューから開ける）。
/// </summary>
public sealed class MacHotkeyService : IDisposable
{
    private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";

    // 'keyb'
    private const uint kEventClassKeyboard = 0x6B657962;
    private const uint kEventHotKeyPressed = 6;

    private readonly LogService _log = LogService.Instance;

    private IntPtr _hotKeyRef;
    private IntPtr _handlerRef;
    private EventHandlerProcPtr? _handler; // GC で回収されないよう保持
    private Action? _onPressed;

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint eventClass;
        public uint eventKind;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyID
    {
        public uint signature;
        public uint id;
    }

    private delegate int EventHandlerProcPtr(IntPtr inHandlerCallRef, IntPtr inEvent, IntPtr inUserData);

    [DllImport(Carbon)]
    private static extern IntPtr GetApplicationEventTarget();

    [DllImport(Carbon)]
    private static extern int InstallEventHandler(
        IntPtr inTarget, EventHandlerProcPtr inHandler, uint inNumTypes,
        EventTypeSpec[] inList, IntPtr inUserData, out IntPtr outRef);

    [DllImport(Carbon)]
    private static extern int RemoveEventHandler(IntPtr inHandlerRef);

    [DllImport(Carbon)]
    private static extern int RegisterEventHotKey(
        uint inHotKeyCode, uint inHotKeyModifiers, EventHotKeyID inHotKeyID,
        IntPtr inTarget, uint inOptions, out IntPtr outRef);

    [DllImport(Carbon)]
    private static extern int UnregisterEventHotKey(IntPtr inHotKey);

    /// <summary>イベントハンドラを1度だけインストールする。</summary>
    public void Initialize(Action onPressed)
    {
        _onPressed = onPressed;
        _handler = HandleHotKey;

        var spec = new[]
        {
            new EventTypeSpec { eventClass = kEventClassKeyboard, eventKind = kEventHotKeyPressed }
        };

        var status = InstallEventHandler(
            GetApplicationEventTarget(), _handler, 1, spec, IntPtr.Zero, out _handlerRef);

        if (status != 0)
            _log.Warn($"InstallEventHandler 失敗 (status={status})。");
    }

    /// <summary>表示文字列を解釈してホットキーを登録する。</summary>
    public bool Register(string display)
    {
        Unregister();

        if (!MacKeyParser.TryParseDisplay(display, out uint mods, out uint keyCode))
        {
            _log.Warn($"ホットキー文字列を解釈できません: '{display}'。");
            return false;
        }

        var id = new EventHotKeyID { signature = 0x53424B59 /*'SBKY'*/, id = 1 };
        var status = RegisterEventHotKey(keyCode, mods, id, GetApplicationEventTarget(), 0, out _hotKeyRef);

        if (status == 0)
        {
            _log.Info($"ホットキー登録に成功 ('{display}' code=0x{keyCode:X} mods=0x{mods:X})。");
            return true;
        }

        _log.Warn($"ホットキー登録に失敗 ('{display}' status={status})。競合の可能性。");
        _hotKeyRef = IntPtr.Zero;
        return false;
    }

    public void Unregister()
    {
        if (_hotKeyRef != IntPtr.Zero)
        {
            UnregisterEventHotKey(_hotKeyRef);
            _hotKeyRef = IntPtr.Zero;
        }
    }

    private int HandleHotKey(IntPtr inHandlerCallRef, IntPtr inEvent, IntPtr inUserData)
    {
        try
        {
            // UI スレッドへ渡す
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _onPressed?.Invoke());
        }
        catch (Exception ex)
        {
            _log.Error("ホットキー処理中に例外。", ex);
        }
        return 0; // noErr
    }

    public void Dispose()
    {
        Unregister();
        if (_handlerRef != IntPtr.Zero)
        {
            RemoveEventHandler(_handlerRef);
            _handlerRef = IntPtr.Zero;
        }
        _handler = null;
    }
}

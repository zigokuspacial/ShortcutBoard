using System.Runtime.InteropServices;

namespace ShortcutBoard.Helpers;

/// <summary>
/// Win32 API の P/Invoke 定義。グローバルホットキー登録に使用。
/// </summary>
internal static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // RegisterHotKey の MOD_NOREPEAT（連続発火を防ぐ）
    public const uint MOD_NOREPEAT = 0x4000;
}

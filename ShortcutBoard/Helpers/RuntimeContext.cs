using System.Runtime.InteropServices;
using System.Text;

namespace ShortcutBoard.Helpers;

/// <summary>
/// 実行環境の判定。MSIX(パッケージ)で動作しているかを返す。
/// WinRT に依存せず Win32 API で判定するため、未パッケージ(GitHub版)でも安全。
/// </summary>
public static class RuntimeContext
{
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    /// <summary>MSIX パッケージとして実行中なら true（GitHub/開発のexe直接実行なら false）。</summary>
    public static bool IsPackaged { get; } = DetectPackaged();

    private static bool DetectPackaged()
    {
        try
        {
            int len = 0;
            int rc = GetCurrentPackageFullName(ref len, null);
            // 未パッケージなら APPMODEL_ERROR_NO_PACKAGE。それ以外(バッファ不足等)はパッケージあり。
            return rc != APPMODEL_ERROR_NO_PACKAGE;
        }
        catch
        {
            return false; // API が無い等は未パッケージ扱い（安全側）
        }
    }
}

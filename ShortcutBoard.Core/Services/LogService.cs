using System.IO;
using System.Text;
using ShortcutBoard.Helpers;

namespace ShortcutBoard.Services;

/// <summary>
/// 簡易ログ出力サービス。日付ごとにファイルへ追記する。（Windows/macOS 共通）
/// 失敗してもアプリを落とさない。
/// </summary>
public class LogService
{
    private static readonly object _lock = new();

    public static LogService Instance { get; } = new LogService();

    private LogService() { }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message, Exception? ex = null)
    {
        var text = ex is null ? message : $"{message}{Environment.NewLine}{ex}";
        Write("ERROR", text);
    }

    private void Write(string level, string message)
    {
        try
        {
            AppPaths.EnsureDirectories();
            var file = Path.Combine(AppPaths.LogsDir, $"shortcutboard-{DateTime.Now:yyyyMMdd}.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(file, line, Encoding.UTF8);
            }
        }
        catch
        {
            // ログ出力自体の失敗は無視（アプリを止めない）
        }
    }
}

using System.Drawing;
using System.Windows.Forms;
using ShortcutBoard.Helpers;

namespace ShortcutBoard.Services;

/// <summary>
/// 通知領域（システムトレイ）のアイコンと右クリックメニューを管理する。
/// System.Windows.Forms.NotifyIcon を使用（標準ライブラリ・無料）。
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly LogService _log = LogService.Instance;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _menu;
    private Icon? _icon;

    // メニュー項目のクリックイベント
    public event Action? OpenListRequested;
    public event Action? OpenEditRequested;
    public event Action? OpenSettingsRequested;
    public event Action? ExitRequested;

    // メニューの開閉（メイン一覧の自動非表示抑制に使用）
    public event Action? MenuOpened;
    public event Action? MenuClosed;

    public void Initialize()
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("一覧を開く(&L)", null, (_, _) => Invoke(OpenListRequested));
        _menu.Items.Add("編集画面を開く(&E)", null, (_, _) => Invoke(OpenEditRequested));
        _menu.Items.Add("設定(&S)", null, (_, _) => Invoke(OpenSettingsRequested));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("終了(&X)", null, (_, _) => Invoke(ExitRequested));

        _menu.Opened += (_, _) => MenuOpened?.Invoke();
        _menu.Closed += (_, _) => MenuClosed?.Invoke();

        _icon = CreateIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Visible = true,
            Text = "ShortcutBoard",
            ContextMenuStrip = _menu,
        };

        // ダブルクリックで一覧を開く
        _notifyIcon.DoubleClick += (_, _) => Invoke(OpenListRequested);

        _log.Info("トレイアイコンを初期化しました。");
    }

    /// <summary>バルーン通知（エラー周知などに使用）。</summary>
    public void ShowBalloon(string title, string message, bool isError = false)
    {
        try
        {
            _notifyIcon?.ShowBalloonTip(
                4000, title, message,
                isError ? ToolTipIcon.Error : ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _log.Error("バルーン通知の表示に失敗。", ex);
        }
    }

    private void Invoke(Action? handler)
    {
        try { handler?.Invoke(); }
        catch (Exception ex) { _log.Error("トレイメニュー処理中に例外。", ex); }
    }

    /// <summary>埋め込みアイコンが無いため、簡単なアイコンを動的生成する。</summary>
    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var bg = new SolidBrush(Color.FromArgb(0x2D, 0x7D, 0xF6));
            g.FillRectangle(bg, 2, 2, 28, 28);
            using var fg = new SolidBrush(Color.White);
            using var font = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("S", font, fg, new RectangleF(2, 2, 28, 28), fmt);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        _menu?.Dispose();
        _icon?.Dispose();
    }
}

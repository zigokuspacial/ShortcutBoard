using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using ShortcutBoard.Helpers;
using ShortcutBoard.Mac.Services;
using ShortcutBoard.Mac.ViewModels;
using ShortcutBoard.Mac.Views;
using ShortcutBoard.Models;
using ShortcutBoard.Services;

namespace ShortcutBoard.Mac;

public partial class App : Application
{
    private readonly LogService _log = LogService.Instance;
    private JsonStorageService _storage = null!;
    private AppSettings _settings = null!;
    private MacMainViewModel _vm = null!;
    private MacStartupService _startup = null!;
    private MacHotkeyService _hotkey = null!;

    private MainWindow _mainWindow = null!;
    private TrayIcon? _tray;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ウィンドウを閉じてもアプリは終了しない（メニューバー常駐）
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                AppPaths.EnsureDirectories();
                _log.Info("==== ShortcutBoard (macOS) 起動 ====");

                _storage = new JsonStorageService();
                _settings = _storage.LoadSettings();
                _startup = new MacStartupService();
                _settings.RunAtStartup = _startup.IsEnabled();

                _vm = new MacMainViewModel(_storage);
                _mainWindow = new MainWindow { DataContext = _vm };

                BuildTray();
                BuildHotkey();
                ShowWelcomeIfFirstRun();
            }
            catch (Exception ex)
            {
                _log.Error("起動処理中に致命的な例外。", ex);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void BuildTray()
    {
        WindowIcon? icon = null;
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://ShortcutBoard/Assets/app.ico"));
            icon = new WindowIcon(stream);
        }
        catch (Exception ex)
        {
            _log.Error("トレイアイコンの読み込みに失敗。", ex);
        }

        var menu = new NativeMenu();

        var miOpen = new NativeMenuItem { Header = "一覧を開く" };
        miOpen.Click += (_, _) => ShowBoard();

        var miSettings = new NativeMenuItem { Header = "設定…" };
        miSettings.Click += (_, _) => OpenSettings();

        var miQuit = new NativeMenuItem { Header = "終了" };
        miQuit.Click += (_, _) => Quit();

        menu.Items.Add(miOpen);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(miSettings);
        menu.Items.Add(miQuit);

        _tray = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "ShortcutBoard",
            IsVisible = true,
            Menu = menu,
        };
        _tray.Clicked += (_, _) => ShowBoard();

        _log.Info("メニューバーアイコンを初期化しました。");
    }

    private void BuildHotkey()
    {
        _hotkey = new MacHotkeyService();
        _hotkey.Initialize(ShowBoard);
        _hotkey.Register(_settings.HotkeyDisplay);
    }

    private void ShowBoard()
    {
        try { _mainWindow.ShowCentered(); }
        catch (Exception ex) { _log.Error("一覧表示に失敗。", ex); }
    }

    private void OpenSettings()
    {
        try
        {
            _mainWindow.SuppressHide = true;
            var svm = new MacSettingsViewModel(_settings);
            var win = new SettingsWindow { DataContext = svm };
            win.Closed += (_, _) =>
            {
                _mainWindow.SuppressHide = false;
                if (win.Result)
                    ApplySettings(svm);
            };
            win.Show();
        }
        catch (Exception ex)
        {
            _log.Error("設定画面の表示に失敗。", ex);
            _mainWindow.SuppressHide = false;
        }
    }

    private void ApplySettings(MacSettingsViewModel svm)
    {
        svm.ApplyTo(_settings);
        _startup.SetEnabled(_settings.RunAtStartup);

        if (!_hotkey.Register(_settings.HotkeyDisplay))
            _log.Warn($"ホットキー '{_settings.HotkeyDisplay}' の登録に失敗（競合の可能性）。");

        _storage.SaveSettings(_settings);
    }

    private void ShowWelcomeIfFirstRun()
    {
        if (_settings.HasShownWelcome) return;
        try
        {
            var title = new TextBlock
            {
                Text = "ShortcutBoardへようこそ！",
                FontSize = 17,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Margin = new Thickness(20, 20, 20, 10),
            };
            var msg = new TextBlock
            {
                Text = "よく使うショートカットキーを、自分用にまとめておけるアプリです。\n\n" +
                       "Ctrl + Shift + A でいつでも一覧を表示できます。\n\n" +
                       "右上の「編集」から、項目の追加・編集・削除ができます。\n" +
                       "まずはサンプルを自分用に書き換えて使ってみてください。",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(20, 0, 20, 16),
            };
            var ok = new Button
            {
                Content = "はじめる",
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20, 0, 20, 20),
                MinWidth = 110,
            };
            var panel = new StackPanel();
            panel.Children.Add(title);
            panel.Children.Add(msg);
            panel.Children.Add(ok);

            var w = new Window
            {
                Title = "ShortcutBoard へようこそ",
                Width = 440,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = panel,
            };
            ok.Click += (_, _) => w.Close();
            w.Show();
        }
        catch (Exception ex)
        {
            _log.Error("初回案内の表示に失敗。", ex);
        }
        finally
        {
            _settings.HasShownWelcome = true;
            try { _storage.SaveSettings(_settings); } catch { /* ignore */ }
        }
    }

    private void Quit()
    {
        try
        {
            _hotkey?.Dispose();
            if (_tray is not null) _tray.IsVisible = false;
            _log.Info("ユーザー操作により終了します。");
        }
        catch { /* ignore */ }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}

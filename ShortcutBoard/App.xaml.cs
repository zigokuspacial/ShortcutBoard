using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ShortcutBoard.Helpers;
using ShortcutBoard.Models;
using ShortcutBoard.Services;
using ShortcutBoard.ViewModels;
using ShortcutBoard.Views;

namespace ShortcutBoard;

/// <summary>
/// アプリ全体の起動・常駐・各サービスの調整を行うエントリポイント。
/// </summary>
public partial class App : Application
{
    private const string MutexName = "Global\\ShortcutBoard_SingleInstance_Mutex";
    private Mutex? _mutex;

    private LogService _log = LogService.Instance;
    private JsonStorageService _storage = null!;
    private AppSettings _settings = null!;
    private MainViewModel _mainVm = null!;

    private HotkeyService _hotkey = null!;
    private TrayIconService _tray = null!;
    private StartupService _startup = null!;

    private MainWindow _mainWindow = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ----- 二重起動防止 -----
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("ShortcutBoard は既に起動しています。",
                "ShortcutBoard", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // ----- 例外で全体が落ちないようにする -----
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _log.Error("未処理例外（AppDomain）。", args.ExceptionObject as Exception);

        try
        {
            AppPaths.EnsureDirectories();
            _log.Info("==== ShortcutBoard 起動 ====");
            _log.Info($"実行ファイル: {Environment.ProcessPath}");
            _log.Info($"データ保存先: {AppPaths.RootDir}");

            _storage = new JsonStorageService();
            _settings = _storage.LoadSettings();
            _startup = new StartupService();

            // 自動起動の同期と自己修復:
            //  - インストーラー等で登録済みなのに設定がOFF → 設定をONに合わせる
            //  - 設定がONなのに登録が消えている → EnsureConsistency が再登録（自己修復）
            //  - 旧方式（レジストリRun）が残っていれば .lnk へ自動移行
            if (!_settings.RunAtStartup && _startup.IsEnabled())
            {
                _settings.RunAtStartup = true;
                _log.Info("自動起動の登録を検出 → 設定をONに同期しました。");
                try { _storage.SaveSettings(_settings); } catch { /* ログ済み */ }
            }
            _startup.EnsureConsistency(_settings.RunAtStartup);

            _mainVm = new MainViewModel(_storage);

            BuildMainWindow();
            BuildTray();
            BuildHotkey();
            ShowWelcomeIfFirstRun();
            StartRecoveryRetryIfNeeded();
        }
        catch (Exception ex)
        {
            _log.Error("起動処理中に致命的な例外。", ex);
            MessageBox.Show($"起動に失敗しました。\n{ex.Message}",
                "ShortcutBoard", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    // ---------------- 構築 ----------------

    private void BuildMainWindow()
    {
        _mainWindow = new MainWindow(_mainVm);
        // 追加・編集はメイン一覧内でインライン実行する。
        // 起動時は非表示（トレイ常駐）。Show せずに待機。
    }

    private void BuildTray()
    {
        _tray = new TrayIconService();
        _tray.OpenListRequested += ShowBoard;
        _tray.OpenEditRequested += () => OpenEditWindow(false);
        _tray.OpenSettingsRequested += OpenSettingsWindow;
        _tray.ExitRequested += ExitApp;
        _tray.MenuOpened += () => _mainWindow.PushSuppressAutoHide();
        _tray.MenuClosed += () => _mainWindow.PopSuppressAutoHide();
        _tray.Initialize();
    }

    private void BuildHotkey()
    {
        _hotkey = new HotkeyService();
        _hotkey.Initialize();
        _hotkey.HotkeyPressed += ShowBoard;

        if (!_hotkey.Register(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey))
        {
            _tray.ShowBalloon(
                "ホットキー登録に失敗",
                $"{_settings.HotkeyDisplay} は他のアプリと競合している可能性があります。設定から変更してください。",
                isError: true);
        }
    }

    private DispatcherTimer? _recoveryTimer;
    private int _recoveryAttempts;

    /// <summary>
    /// 起動時にデータが一切読めず空で起動した場合（一時的なFS異常を想定）、
    /// 10秒間隔でディスクの再読込を試み、実データが見えるようになり次第反映する。
    /// </summary>
    private void StartRecoveryRetryIfNeeded()
    {
        if (!_storage.LastLoadUsedFallback || _mainVm.AllShortcuts.Count > 0)
            return; // 異常時（空起動）のみ対象。初回起動のサンプル投入は対象外。

        _log.Warn("データ自動復元の再試行を開始します（10秒間隔・最大30回）。");
        _recoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _recoveryTimer.Tick += (_, _) =>
        {
            _recoveryAttempts++;
            try
            {
                _mainVm.Reload();
                if (!_storage.LastLoadUsedFallback && _mainVm.AllShortcuts.Count > 0)
                {
                    _recoveryTimer!.Stop();
                    _log.Info($"データの自動復元に成功しました（{_mainVm.AllShortcuts.Count} 件、{_recoveryAttempts} 回目）。");
                    _tray?.ShowBalloon("ShortcutBoard",
                        $"ショートカットデータを読み込みました（{_mainVm.AllShortcuts.Count} 件）。");
                    return;
                }
            }
            catch (Exception ex)
            {
                _log.Error("データ自動復元の再試行中に例外。", ex);
            }

            if (_recoveryAttempts >= 30) // 約5分で打ち切り
            {
                _recoveryTimer!.Stop();
                _log.Error("データの自動復元に失敗しました（再試行上限）。ログを確認してください。");
                _tray?.ShowBalloon("ShortcutBoard",
                    "ショートカットデータを読み込めませんでした。ログを確認してください。", isError: true);
            }
        };
        _recoveryTimer.Start();
    }

    /// <summary>初回起動時に一度だけ、オンボーディングポップアップを表示する。</summary>
    private void ShowWelcomeIfFirstRun()
    {
        if (_settings.HasShownWelcome)
        {
            _log.Info("初回オンボーディング: 表示済みのためスキップ。");
            return;
        }
        try
        {
            var win = new WelcomeWindow();
            win.Show();   // 非モーダル（軽く・邪魔にならない）
            win.Activate();
            _log.Info("初回オンボーディングを表示しました。");
        }
        catch (Exception ex)
        {
            _log.Error("初回案内の表示に失敗。", ex);
        }
        finally
        {
            // 一度表示したらフラグを保存し、次回以降は表示しない
            _settings.HasShownWelcome = true;
            try { _storage.SaveSettings(_settings); } catch { /* ignore */ }
        }
    }

    // ---------------- 動作 ----------------

    /// <summary>メイン一覧を画面中央に表示する。</summary>
    private void ShowBoard()
    {
        try
        {
            _mainWindow.ShowAtCenter();
        }
        catch (Exception ex)
        {
            _log.Error("一覧表示に失敗。", ex);
        }
    }

    private void OpenEditWindow(bool startWithNew)
    {
        try
        {
            _mainWindow.PushSuppressAutoHide();
            var editVm = new EditViewModel(_storage);
            editVm.Saved += () => _mainVm.Reload();
            if (startWithNew)
                editVm.AddCommand.Execute(null); // 新規行を追加して選択状態で開く
            var win = new EditWindow(editVm)
            {
                Owner = _mainWindow.IsVisible ? _mainWindow : null,
                WindowStartupLocation = _mainWindow.IsVisible
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen,
            };
            win.ShowDialog();
        }
        catch (Exception ex)
        {
            _log.Error("編集画面の表示に失敗。", ex);
        }
        finally
        {
            _mainWindow.PopSuppressAutoHide();
        }
    }

    private void OpenSettingsWindow()
    {
        try
        {
            _mainWindow.PushSuppressAutoHide();
            var settingsVm = new SettingsViewModel(_settings);
            var win = new SettingsWindow(settingsVm)
            {
                Owner = _mainWindow.IsVisible ? _mainWindow : null,
                WindowStartupLocation = _mainWindow.IsVisible
                    ? WindowStartupLocation.CenterOwner
                    : WindowStartupLocation.CenterScreen,
            };

            if (win.ShowDialog() == true)
            {
                ApplySettings(settingsVm);
            }
        }
        catch (Exception ex)
        {
            _log.Error("設定画面の表示に失敗。", ex);
        }
        finally
        {
            _mainWindow.PopSuppressAutoHide();
        }
    }

    private void ApplySettings(SettingsViewModel vm)
    {
        var oldMod = _settings.HotkeyModifiers;
        var oldVk = _settings.HotkeyVirtualKey;

        vm.ApplyTo(_settings);

        // 自動起動の反映
        _startup.SetEnabled(_settings.RunAtStartup);

        // ホットキー再登録
        if (!_hotkey.Register(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey))
        {
            _tray.ShowBalloon(
                "ホットキー登録に失敗",
                $"{_settings.HotkeyDisplay} は他のアプリと競合している可能性があります。",
                isError: true);
            // 失敗時は元に戻して再登録を試みる
            _settings.HotkeyModifiers = oldMod;
            _settings.HotkeyVirtualKey = oldVk;
            _hotkey.Register(oldMod, oldVk);
        }

        _storage.SaveSettings(_settings);
    }

    private void ExitApp()
    {
        _log.Info("ユーザー操作により終了します。");
        Shutdown();
    }

    // ---------------- 例外・終了 ----------------

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log.Error("未処理例外（Dispatcher）。アプリは継続します。", e.Exception);
        e.Handled = true; // アプリを落とさない
        try
        {
            _tray?.ShowBalloon("エラー", "予期しないエラーが発生しました。ログを確認してください。", isError: true);
        }
        catch { /* ignore */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            // 終了時にも保存する。ただし直近のロードがフォールバック（サンプル等）だった
            // 場合は、実データをサンプルで上書きしないようスキップする。
            if (_mainVm is not null && _storage is not null && !_storage.LastLoadUsedFallback)
            {
                try
                {
                    // 編集途中の未確定行（IsNew）は保存対象から除外
                    _storage.SaveShortcuts(_mainVm.AllShortcuts.Where(s => !s.IsNew));
                    _log.Info("終了時保存が完了しました。");
                }
                catch (Exception ex)
                {
                    _log.Error("終了時保存に失敗。", ex);
                }
            }

            _hotkey?.Dispose();
            _tray?.Dispose();
            _log.Info("==== ShortcutBoard 終了 ====");
        }
        catch { /* ignore */ }
        finally
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        base.OnExit(e);
    }
}

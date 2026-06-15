using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using ShortcutBoard.Helpers;
using Windows.ApplicationModel;

namespace ShortcutBoard.Services;

/// <summary>
/// 自動起動を管理する。実行環境で方式を切り替える。
///  - 未パッケージ（GitHub/開発のexe直接実行）: スタートアップフォルダ（shell:startup）への .lnk
///  - MSIX パッケージ（Microsoft Store版）: windows.startupTask 拡張 ＋ StartupTask API
/// 公開メソッド（IsEnabled / SetEnabled / EnsureConsistency）は実行環境に応じて内部で分岐する。
/// </summary>
public class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ShortcutBoard";

    // AppxManifest.xml の <uap5:StartupTask TaskId="..."> と一致させること
    private const string StartupTaskId = "ShortcutBoardStartup";

    private readonly LogService _log = LogService.Instance;

    private static bool Packaged => RuntimeContext.IsPackaged;

    private static string StartupFolder =>
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    /// <summary>スタートアップフォルダ内のショートカットのパス。</summary>
    public static string ShortcutLinkPath => Path.Combine(StartupFolder, "ShortcutBoard.lnk");

    /// <summary>現在自動起動が有効か。</summary>
    public bool IsEnabled() => Packaged ? PkgIsEnabled() : (LinkExists() || HasLegacyRunValue());

    /// <summary>.lnk 方式で登録済みか。</summary>
    public bool LinkExists()
    {
        try { return File.Exists(ShortcutLinkPath); }
        catch { return false; }
    }

    public void SetEnabled(bool enabled)
    {
        if (Packaged) { PkgSetEnabled(enabled); return; }

        if (enabled)
        {
            var ok = CreateStartupLink();
            // 二重起動を防ぐため旧レジストリ値は常に掃除
            RemoveLegacyRunValue();
            if (ok)
                _log.Info($"自動起動を有効化（スタートアップフォルダ）: {ShortcutLinkPath}");
            else
                _log.Error($"自動起動の登録に失敗: {ShortcutLinkPath}");
        }
        else
        {
            try
            {
                if (LinkExists()) File.Delete(ShortcutLinkPath);
                RemoveLegacyRunValue();
                _log.Info("自動起動を無効化しました。");
            }
            catch (Exception ex)
            {
                _log.Error("自動起動の解除に失敗。", ex);
            }
        }
    }

    /// <summary>
    /// 起動時の整合性チェック。
    ///  - 旧レジストリ方式が残っていれば .lnk へ移行
    ///  - 設定がONなのに登録が消えていれば自己修復
    /// </summary>
    /// <returns>処理後に自動起動が有効なら true。</returns>
    public bool EnsureConsistency(bool settingsSayEnabled)
    {
        if (Packaged) return PkgEnsureConsistency(settingsSayEnabled);
        try
        {
            // 旧方式からの移行
            if (HasLegacyRunValue())
            {
                _log.Info("旧方式（レジストリRun）の自動起動を検出 → スタートアップフォルダ方式へ移行します。");
                CreateStartupLink();
                RemoveLegacyRunValue();
            }

            bool registered = LinkExists();

            if (settingsSayEnabled && !registered)
            {
                _log.Warn("自動起動設定がONなのに登録が見つかりません → 自己修復します。");
                CreateStartupLink();
                registered = LinkExists();
                _log.Info(registered ? "自己修復に成功しました。" : "自己修復に失敗しました。");
            }
            else if (settingsSayEnabled && registered)
            {
                // .lnk のリンク先が現在の実行ファイルと異なる場合は作り直す
                // （アプリの更新・移動、開発版⇔インストール版の切替に追従）
                var target = GetLinkTarget();
                var current = GetExecutablePath();
                if (!string.IsNullOrEmpty(target) && !string.IsNullOrEmpty(current)
                    && !string.Equals(target, current, StringComparison.OrdinalIgnoreCase))
                {
                    _log.Warn($".lnk のリンク先が現在の実行ファイルと異なるため再作成します。旧: {target} → 新: {current}");
                    CreateStartupLink();
                }
            }

            _log.Info($"自動起動状態: 設定={settingsSayEnabled}, 登録={registered}, 方式=スタートアップフォルダ");
            return registered;
        }
        catch (Exception ex)
        {
            _log.Error("自動起動の整合性チェックに失敗。", ex);
            return IsEnabled();
        }
    }

    // ---------------- MSIX(StartupTask) 用 ----------------

    private bool PkgIsEnabled()
    {
        try
        {
            var task = StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult();
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        catch (Exception ex)
        {
            _log.Error("StartupTask の状態取得に失敗。", ex);
            return false;
        }
    }

    private void PkgSetEnabled(bool enabled)
    {
        try
        {
            var task = StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult();
            if (enabled)
            {
                if (task.State == StartupTaskState.Disabled)
                {
                    var res = task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                    _log.Info($"自動起動(StartupTask)を有効化要求 → {res}");
                }
                else
                {
                    _log.Info($"自動起動(StartupTask)は既に {task.State}。");
                }
            }
            else
            {
                if (task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
                {
                    task.Disable();
                    _log.Info("自動起動(StartupTask)を無効化しました。");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error("自動起動(StartupTask)の設定に失敗。", ex);
        }
    }

    private bool PkgEnsureConsistency(bool settingsSayEnabled)
    {
        try
        {
            var task = StartupTask.GetAsync(StartupTaskId).AsTask().GetAwaiter().GetResult();
            var enabled = task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;

            // 設定ONなのにユーザー無効化以外で無効なら、有効化を試みる（自己修復）
            if (settingsSayEnabled && task.State == StartupTaskState.Disabled)
            {
                var res = task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                enabled = res is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
                _log.Info($"自動起動(StartupTask)自己修復 → {res}");
            }

            _log.Info($"自動起動状態: 設定={settingsSayEnabled}, 登録={enabled}, 方式=StartupTask(MSIX), 実状態={task.State}");
            return enabled;
        }
        catch (Exception ex)
        {
            _log.Error("StartupTask の整合性チェックに失敗。", ex);
            return false;
        }
    }

    // ---------------- 内部処理 (.lnk / レジストリ) ----------------

    /// <summary>WScript.Shell COM で .lnk を作成する（外部ライブラリ不要）。</summary>
    private bool CreateStartupLink()
    {
        try
        {
            var exe = GetExecutablePath();
            if (string.IsNullOrEmpty(exe))
            {
                _log.Error("実行ファイルパスを取得できないため .lnk を作成できません。");
                return false;
            }

            Directory.CreateDirectory(StartupFolder);

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                _log.Error("WScript.Shell を利用できません。");
                return false;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(ShortcutLinkPath);
            link.TargetPath = exe;
            link.WorkingDirectory = Path.GetDirectoryName(exe) ?? "";
            link.Description = "ShortcutBoard - ショートカット一覧";
            link.Save();

            return LinkExists();
        }
        catch (Exception ex)
        {
            _log.Error(".lnk の作成に失敗。", ex);
            return false;
        }
    }

    /// <summary>既存 .lnk のリンク先を取得する（取得できなければ空文字）。</summary>
    private string GetLinkTarget()
    {
        try
        {
            if (!LinkExists()) return string.Empty;
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return string.Empty;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(ShortcutLinkPath);
            return (string)(link.TargetPath ?? string.Empty);
        }
        catch (Exception ex)
        {
            _log.Error(".lnk のリンク先取得に失敗。", ex);
            return string.Empty;
        }
    }

    private bool HasLegacyRunValue()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string s && !string.IsNullOrEmpty(s);
        }
        catch (Exception ex)
        {
            _log.Error("旧レジストリ値の確認に失敗。", ex);
            return false;
        }
    }

    private void RemoveLegacyRunValue()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _log.Info("旧方式（レジストリRun）の登録を削除しました。");
            }
        }
        catch (Exception ex)
        {
            _log.Error("旧レジストリ値の削除に失敗。", ex);
        }
    }

    private static string GetExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path)) return path;
        return Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
    }
}

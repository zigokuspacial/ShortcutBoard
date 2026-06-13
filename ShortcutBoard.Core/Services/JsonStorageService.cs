using System.IO;
using System.Text.Json;
using ShortcutBoard.Data;
using ShortcutBoard.Helpers;
using ShortcutBoard.Models;

namespace ShortcutBoard.Services;

/// <summary>
/// ショートカット一覧と設定の JSON 読み書きを担当する。（Windows/macOS 共通）
///
/// データ保護の多層防御:
///  1. 保存は一時ファイル経由のアトミック書き込み（途中クラッシュで壊れない）
///  2. 保存成功のたびに shortcuts.backup.json（最終正常版）を更新
///  3. 読み込み成功時、1日1回 backups/shortcuts-YYYYMMDD.json スナップショットを保存（7世代）
///  4. メインが「見つからない/壊れている」場合はバックアップから自動復元
///  5. サンプルデータの作成は「メインもバックアップも存在しない真の初回」のみ。
///     既存データをサンプルで上書きすることは決してない。
/// </summary>
public class JsonStorageService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly LogService _log = LogService.Instance;

    /// <summary>
    /// 直近の LoadShortcuts がフォールバック（サンプル作成・異常時の空起動）だった場合 true。
    /// 終了時保存などで実データを巻き込まないための判定に使う。
    /// </summary>
    public bool LastLoadUsedFallback { get; private set; }

    /// <summary>
    /// 「初回起動かどうか」の判定材料。インスタンス生成時点で settings.json が
    /// 存在していたなら、過去に起動したことがある＝初回ではない。
    /// （データファイルが一時的に見えない異常時に、サンプルで上書きするのを防ぐ）
    /// </summary>
    private readonly bool _settingsExistedAtStart = SafeExists(AppPaths.SettingsFile);

    // ---------- ショートカット ----------

    public List<Shortcut> LoadShortcuts()
    {
        AppPaths.EnsureDirectories();
        var path = AppPaths.ShortcutsFile;
        LastLoadUsedFallback = false;

        bool mainReadable = TryReadFile(path, out var main, out var mainError);

        // 1) メインが実データ（サンプル以外）として読めれば、それが正
        if (mainReadable && !SampleData.IsSample(main!))
        {
            _log.Info($"shortcuts.json の読み込みに成功（{main!.Count} 件）: {path}");
            TakeDailySnapshot(path);
            return main;
        }

        // 2) メインが「サンプルそのもの」または読めない場合、
        //    実データを持つ復旧元（バックアップ → スナップショット → 同梱復旧ファイル）を探す
        foreach (var (label, file) in RecoveryCandidates())
        {
            if (file is null) continue;
            if (TryReadFile(file, out var cand, out _) && !SampleData.IsSample(cand!))
            {
                if (mainReadable)
                {
                    // メインはサンプル＝過去の誤上書きの可能性 → 退避してから実データを復元
                    RetireFile(path, "sample-replaced");
                    _log.Warn($"shortcuts.json がサンプルデータでした。{label}から実データを復元します（{cand!.Count} 件）: {file}");
                }
                else
                {
                    if (SafeExists(path))
                    {
                        _log.Error($"shortcuts.json が破損しています: {mainError}");
                        BackupCorruptedFile(path);
                    }
                    else
                    {
                        _log.Warn($"shortcuts.json が見つかりません: {path}");
                    }
                    _log.Warn($"{label}から自動復元します（{cand!.Count} 件）: {file}");
                }

                try { SaveShortcuts(cand); }
                catch (Exception ex) { _log.Error("復元の書き戻しに失敗（メモリ上のデータで続行）。", ex); }
                return cand;
            }
        }

        // 3) メインがサンプルとして読めて、実データ源がどこにも無い
        //    ＝初回起動後そのまま使っている正常な状態
        if (mainReadable)
        {
            _log.Info($"shortcuts.json の読み込みに成功（{main!.Count} 件・サンプル構成）: {path}");
            return main!;
        }

        // 4) メインが読めない（無い/破損）
        if (SafeExists(path))
        {
            _log.Error($"shortcuts.json が破損しています: {mainError}");
            BackupCorruptedFile(path);
        }
        else
        {
            _log.Warn($"shortcuts.json が見つかりません: {path}");
        }

        // サンプル内容でも構わないので残っている復旧元があれば使う（破損からの復旧）
        foreach (var (label, file) in RecoveryCandidates())
        {
            if (file is null) continue;
            if (TryReadFile(file, out var cand, out _))
            {
                _log.Warn($"{label}から自動復元します（{cand!.Count} 件）: {file}");
                try { SaveShortcuts(cand); } catch (Exception ex) { _log.Error("復元の書き戻しに失敗。", ex); }
                return cand;
            }
        }

        // 5) どこからも読めない
        if (!_settingsExistedAtStart)
        {
            // 真の初回起動（settings.json も無かった）→ サンプルを作成
            _log.Info("初回起動と判定（settings.json・データ・バックアップすべて無し）。サンプルデータを作成します。");
            LastLoadUsedFallback = true;
            var sample = SampleData.Create();
            try { SaveShortcuts(sample); }
            catch (Exception ex) { _log.Error("サンプルデータの保存に失敗（メモリ上のデータで続行）。", ex); }
            return sample;
        }

        // 既存環境なのにデータが一切見えない＝一時的なファイルシステム異常の可能性。
        // 何もディスクへ書かず、空の状態で起動する（呼び出し側が後で再試行する）。
        _log.Error("既存環境ですがデータファイルが一切読めません（一時的な異常の可能性）。" +
                   "ディスクへは何も書き込まず、空の状態で起動して自動再試行します。");
        LastLoadUsedFallback = true;
        return new List<Shortcut>();
    }

    /// <summary>実データの復旧元候補（優先順）。</summary>
    private IEnumerable<(string label, string? file)> RecoveryCandidates()
    {
        yield return ("バックアップ", AppPaths.BackupFile);
        yield return ("日次スナップショット", FindLatestSnapshot());
        yield return ("同梱の復旧用ファイル", GetBundledRecoveryFile());
    }

    /// <summary>実行ファイルと同じフォルダに置かれた復旧用データ（インストーラー同梱）。</summary>
    private static string? GetBundledRecoveryFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(Environment.ProcessPath);
            if (string.IsNullOrEmpty(dir)) return null;
            var file = Path.Combine(dir, "recovery-shortcuts.json");
            return File.Exists(file) ? file : null;
        }
        catch { return null; }
    }

    /// <summary>ファイルをタイムスタンプ付きで退避する（削除はしない）。</summary>
    private void RetireFile(string path, string reason)
    {
        try
        {
            if (!File.Exists(path)) return;
            var retired = $"{path}.{reason}-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(path, retired, overwrite: true);
            _log.Info($"既存ファイルを退避しました: {retired}");
        }
        catch (Exception ex)
        {
            _log.Error("ファイルの退避に失敗。", ex);
        }
    }

    public void SaveShortcuts(IEnumerable<Shortcut> shortcuts)
    {
        try
        {
            AppPaths.EnsureDirectories();
            var listToSave = shortcuts.ToList();
            var json = JsonSerializer.Serialize(listToSave, Options);
            AtomicWrite(AppPaths.ShortcutsFile, json);
            _log.Info($"ショートカットを保存しました（{listToSave.Count} 件）: {AppPaths.ShortcutsFile}");

            // 最終正常版バックアップを更新。
            // ただし「保存内容がサンプルそのもの」で「既存バックアップに実データがある」場合は
            // 上書きしない（実データの最後の砦を守る）。
            try
            {
                bool skipBackup = SampleData.IsSample(listToSave)
                    && TryReadFile(AppPaths.BackupFile, out var existingBackup, out _)
                    && !SampleData.IsSample(existingBackup!);

                if (skipBackup)
                {
                    _log.Warn("保存内容がサンプルデータのため、実データ入りバックアップの上書きをスキップしました。");
                }
                else
                {
                    File.Copy(AppPaths.ShortcutsFile, AppPaths.BackupFile, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                _log.Error("バックアップ(shortcuts.backup.json)の更新に失敗。", ex);
            }
        }
        catch (Exception ex)
        {
            _log.Error("shortcuts.json の保存に失敗しました。", ex);
            throw;
        }
    }

    // ---------- 設定 ----------

    public AppSettings LoadSettings()
    {
        AppPaths.EnsureDirectories();
        var path = AppPaths.SettingsFile;

        if (!SafeExists(path))
        {
            _log.Info($"settings.json が無いため既定値で作成します: {path}");
            var def = new AppSettings();
            try { SaveSettings(def); }
            catch (Exception ex) { _log.Error("既定設定の保存に失敗。", ex); }
            return def;
        }

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            _log.Info($"settings.json の読み込みに成功: {path}");
            return loaded;
        }
        catch (Exception ex)
        {
            _log.Error("settings.json の読み込みに失敗しました。既定値で復旧します。", ex);
            BackupCorruptedFile(path);
            var def = new AppSettings();
            try { SaveSettings(def); } catch { /* ignore */ }
            return def;
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            AppPaths.EnsureDirectories();
            var json = JsonSerializer.Serialize(settings, Options);
            AtomicWrite(AppPaths.SettingsFile, json);
            _log.Info("settings.json を保存しました。");
        }
        catch (Exception ex)
        {
            _log.Error("settings.json の保存に失敗しました。", ex);
            throw;
        }
    }

    // ---------- 内部処理 ----------

    /// <summary>ファイルを読み、ショートカット一覧として解釈できれば true。</summary>
    private static bool TryReadFile(string path, out List<Shortcut>? list, out string? error)
    {
        list = null;
        error = null;
        try
        {
            if (!File.Exists(path)) { error = "ファイルが存在しません"; return false; }
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<List<Shortcut>>(json, Options);
            if (parsed is null) { error = "デシリアライズ結果が null"; return false; }

            foreach (var s in parsed)
            {
                if (string.IsNullOrWhiteSpace(s.Id))
                    s.Id = Guid.NewGuid().ToString("N");
            }
            list = parsed;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool SafeExists(string path)
    {
        try { return File.Exists(path); }
        catch { return false; }
    }

    /// <summary>1日1回、読み込み成功時点のデータをスナップショットとして保存（7世代保持）。</summary>
    private void TakeDailySnapshot(string sourcePath)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.SnapshotsDir);
            var snap = Path.Combine(AppPaths.SnapshotsDir, $"shortcuts-{DateTime.Now:yyyyMMdd}.json");
            if (File.Exists(snap)) return;

            File.Copy(sourcePath, snap);
            _log.Info($"日次スナップショットを作成: {snap}");

            // 古いものを削除（新しい7件を残す）
            var old = Directory.GetFiles(AppPaths.SnapshotsDir, "shortcuts-*.json")
                .OrderByDescending(f => f)
                .Skip(7)
                .ToList();
            foreach (var f in old) File.Delete(f);
        }
        catch (Exception ex)
        {
            _log.Error("日次スナップショットの作成に失敗。", ex);
        }
    }

    private string? FindLatestSnapshot()
    {
        try
        {
            if (!Directory.Exists(AppPaths.SnapshotsDir)) return null;
            return Directory.GetFiles(AppPaths.SnapshotsDir, "shortcuts-*.json")
                .OrderByDescending(f => f)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    private void BackupCorruptedFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var backup = $"{path}.corrupted-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
            File.Copy(path, backup, overwrite: true);
            _log.Warn($"破損ファイルをバックアップしました: {backup}");
        }
        catch (Exception ex)
        {
            _log.Error("破損ファイルのバックアップに失敗しました。", ex);
        }
    }

    /// <summary>
    /// 一時ファイルへ書いてから置き換える（書き込み途中での破損を防ぐ）。
    /// AV やバックアップソフトの一時ロックに備えて短いリトライを行う。
    /// </summary>
    private static void AtomicWrite(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);

        const int maxAttempts = 3;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100);
            }
        }
    }
}

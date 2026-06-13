using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using ShortcutBoard.Helpers;
using ShortcutBoard.Models;
using ShortcutBoard.Services;

namespace ShortcutBoard.ViewModels;

/// <summary>
/// メイン一覧ウィンドウの ViewModel。
/// 一覧表示に加え、行を直接インラインで追加・編集・削除する。
/// </summary>
public class MainViewModel : ObservableObject
{
    // 共通ロジック（ShortcutBoard.Core）と同じ値を使用
    public const string AllCategories = "すべて";

    private readonly JsonStorageService _storage;
    private readonly LogService _log = LogService.Instance;

    public ObservableCollection<Shortcut> AllShortcuts { get; } = new();

    public ICollectionView ShortcutsView { get; }

    public ObservableCollection<string> Categories { get; } = new();

    /// <summary>編集欄のカテゴリー候補チップ。</summary>
    public ObservableCollection<string> CategorySuggestions { get; } = new()
    {
        "Windows", "Chrome", "PowerToys", "Claude", "ChatGPT", "自作",
    };

    public RelayCommand AddCommand { get; }
    public RelayCommand EditSelectedCommand { get; }
    public RelayCommand EditItemCommand { get; }
    public RelayCommand SaveItemCommand { get; }
    public RelayCommand DeleteItemCommand { get; }
    public RelayCommand CancelItemCommand { get; }
    public RelayCommand SetCategoryCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand ToggleManageCommand { get; }

    private bool _isManageMode;
    /// <summary>管理モード（ON のとき各行に並べ替え・編集・削除ボタンを表示）。</summary>
    public bool IsManageMode
    {
        get => _isManageMode;
        set
        {
            if (!SetProperty(ref _isManageMode, value)) return;
            if (!value) EndAllEdits();           // 管理モードを抜けたら編集も終了
            OnPropertyChanged(nameof(ManageButtonLabel));
            OnPropertyChanged(nameof(IsAnyEditing));
            ShortcutsView.Refresh();
        }
    }

    public string ManageButtonLabel => IsManageMode ? "完了" : "編集";

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ShortcutsView.Refresh(); }
    }

    private string _selectedCategory = AllCategories;
    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetProperty(ref _selectedCategory, value)) ShortcutsView.Refresh(); }
    }

    private Shortcut? _selectedShortcut;
    public Shortcut? SelectedShortcut
    {
        get => _selectedShortcut;
        set => SetProperty(ref _selectedShortcut, value);
    }

    /// <summary>いずれかの行がインライン編集中か。</summary>
    public bool IsAnyEditing => AllShortcuts.Any(s => s.IsEditing);

    public MainViewModel(JsonStorageService storage)
    {
        _storage = storage;
        ShortcutsView = CollectionViewSource.GetDefaultView(AllShortcuts);
        ShortcutsView.Filter = FilterPredicate;

        AddCommand = new RelayCommand(_ => AddNew());
        EditSelectedCommand = new RelayCommand(_ => BeginEdit(SelectedShortcut));
        EditItemCommand = new RelayCommand(p => BeginEdit(p as Shortcut));
        SaveItemCommand = new RelayCommand(p => SaveItem(p as Shortcut));
        DeleteItemCommand = new RelayCommand(p => DeleteItem(p as Shortcut));
        CancelItemCommand = new RelayCommand(p => CancelEdit(p as Shortcut));
        SetCategoryCommand = new RelayCommand(SetCategoryOnEditing);
        MoveUpCommand = new RelayCommand(p => Move(p as Shortcut, -1));
        MoveDownCommand = new RelayCommand(p => Move(p as Shortcut, +1));
        ToggleManageCommand = new RelayCommand(_ => IsManageMode = !IsManageMode);

        Reload();
    }

    /// <summary>ディスクから読み直して一覧を更新する。</summary>
    public void Reload()
    {
        var items = _storage.LoadShortcuts();
        AllShortcuts.Clear();
        // 保存された順序をそのまま保持する（手動の並び替えを反映するため並べ替えない）
        foreach (var s in items)
            AllShortcuts.Add(s);
        RebuildCategories();
        ShortcutsView.Refresh();
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    // ---------------- インライン編集 ----------------

    /// <summary>編集中の行を全て閉じる（新規未保存は破棄）。単一編集を保つため。</summary>
    private void EndAllEdits()
    {
        foreach (var s in AllShortcuts.Where(x => x.IsEditing).ToList())
        {
            if (s.IsNew) AllShortcuts.Remove(s);
            else s.IsEditing = false;
        }
    }

    private void AddNew()
    {
        EndAllEdits();

        // 新規行が必ず見えるようにフィルターを解除
        SearchText = string.Empty;
        SelectedCategory = AllCategories;

        var s = new Shortcut
        {
            Keys = string.Empty,
            Name = string.Empty,
            Description = string.Empty,
            Category = "自作",
            IsNew = true,
            IsEditing = true,
        };
        AllShortcuts.Insert(0, s);
        SelectedShortcut = s;
        ShortcutsView.Refresh();
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    private void BeginEdit(Shortcut? s)
    {
        if (s is null) return;
        EndAllEdits();
        s.IsEditing = true;
        SelectedShortcut = s;
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    private void SaveItem(Shortcut? s)
    {
        if (s is null) return;

        // キーも名前も空ならキャンセル扱い
        if (string.IsNullOrWhiteSpace(s.Keys) && string.IsNullOrWhiteSpace(s.Name))
        {
            CancelEdit(s);
            return;
        }

        s.IsEditing = false;
        s.IsNew = false;
        PersistAll();
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    // 直近に削除した項目（元に戻す用）
    private Shortcut? _lastDeleted;
    private int _lastDeletedIndex = -1;

    private void DeleteItem(Shortcut? s)
    {
        if (s is null) return;
        _lastDeletedIndex = AllShortcuts.IndexOf(s);
        _lastDeleted = s;
        AllShortcuts.Remove(s);
        PersistAll();
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    /// <summary>直近に削除した1件を元の位置へ復元する。</summary>
    public bool UndoLastDelete()
    {
        if (_lastDeleted is null) return false;

        var idx = (_lastDeletedIndex >= 0 && _lastDeletedIndex <= AllShortcuts.Count)
            ? _lastDeletedIndex
            : AllShortcuts.Count;
        AllShortcuts.Insert(idx, _lastDeleted);

        _log.Info($"削除した項目を元に戻しました: {_lastDeleted.Name}");
        _lastDeleted = null;
        _lastDeletedIndex = -1;
        PersistAll();
        OnPropertyChanged(nameof(IsAnyEditing));
        return true;
    }

    private void CancelEdit(Shortcut? s)
    {
        if (s is null) return;
        if (s.IsNew)
        {
            // 未保存の新規行は破棄
            AllShortcuts.Remove(s);
        }
        else
        {
            s.IsEditing = false;
            // 既存行の編集キャンセルはディスクから読み直して元に戻す
            Reload();
            return;
        }
        ShortcutsView.Refresh();
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    /// <summary>行を1つ上(-1)／下(+1)へ移動して順序を保存する。</summary>
    private void Move(Shortcut? s, int direction)
    {
        if (s is null) return;
        int index = AllShortcuts.IndexOf(s);
        if (index < 0) return;
        int target = index + direction;
        if (target < 0 || target >= AllShortcuts.Count) return;

        AllShortcuts.Move(index, target);
        SelectedShortcut = s;
        PersistAll();
    }

    /// <summary>編集中の行にカテゴリー候補チップの値を入れる。</summary>
    private void SetCategoryOnEditing(object? param)
    {
        if (param is string cat && SelectedShortcut is { IsEditing: true } s)
            s.Category = cat;
    }

    private void PersistAll()
    {
        try
        {
            _storage.SaveShortcuts(AllShortcuts);
            RebuildCategories();
            ShortcutsView.Refresh();
        }
        catch (Exception ex)
        {
            _log.Error("一覧の保存に失敗しました。", ex);
        }
    }

    // ---------------- カテゴリー / フィルター ----------------

    private void RebuildCategories()
    {
        // カテゴリー一覧の生成は共通ロジックを使用
        var cats = ShortcutSearch.BuildCategoryList(AllShortcuts);

        var current = SelectedCategory;
        Categories.Clear();
        foreach (var c in cats) Categories.Add(c);

        _selectedCategory = Categories.Contains(current) ? current : AllCategories;
        OnPropertyChanged(nameof(SelectedCategory));
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not Shortcut s) return false;

        // 編集中の行は常に表示する（フィルターで消えないように）
        if (s.IsEditing) return true;

        // 検索・絞り込みの判定は共通ロジック（ShortcutBoard.Core）を使用
        return ShortcutSearch.Matches(s, SearchText, SelectedCategory);
    }
}

using System.Collections.ObjectModel;
using ShortcutBoard.Helpers;
using ShortcutBoard.Mac.Helpers;
using ShortcutBoard.Models;
using ShortcutBoard.Services;
using ShortcutBoard.ViewModels;

namespace ShortcutBoard.Mac.ViewModels;

/// <summary>
/// macOS 版メイン一覧の ViewModel。Windows 版と同等の機能を Avalonia 向けに実装。
/// 保存・検索・並べ替えのロジックは ShortcutBoard.Core を共有する。
/// </summary>
public class MacMainViewModel : ObservableObject
{
    private readonly JsonStorageService _storage;
    private readonly LogService _log = LogService.Instance;

    private readonly List<Shortcut> _all = new();

    /// <summary>画面に表示する（フィルター適用後の）一覧。</summary>
    public ObservableCollection<Shortcut> Items { get; } = new();

    public ObservableCollection<string> Categories { get; } = new();

    public ObservableCollection<string> CategorySuggestions { get; } = new()
    {
        "Windows", "Chrome", "PowerToys", "Claude", "ChatGPT", "自作",
    };

    public RelayCommand AddCommand { get; }
    public RelayCommand EditItemCommand { get; }
    public RelayCommand SaveItemCommand { get; }
    public RelayCommand DeleteItemCommand { get; }
    public RelayCommand CancelItemCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand ToggleManageCommand { get; }
    public RelayCommand SetCategoryCommand { get; }

    private bool _isManageMode;
    public bool IsManageMode
    {
        get => _isManageMode;
        set
        {
            if (!SetProperty(ref _isManageMode, value)) return;
            if (!value) EndAllEdits();
            OnPropertyChanged(nameof(ManageButtonLabel));
            OnPropertyChanged(nameof(IsAnyEditing));
            RefreshView();
        }
    }

    public string ManageButtonLabel => IsManageMode ? "完了" : "編集";

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) RefreshView(); }
    }

    private string _selectedCategory = ShortcutSearch.AllCategories;
    public string SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetProperty(ref _selectedCategory, value)) RefreshView(); }
    }

    private Shortcut? _selectedShortcut;
    public Shortcut? SelectedShortcut
    {
        get => _selectedShortcut;
        set => SetProperty(ref _selectedShortcut, value);
    }

    public bool IsAnyEditing => _all.Any(s => s.IsEditing);

    public MacMainViewModel(JsonStorageService storage)
    {
        _storage = storage;

        AddCommand = new RelayCommand(_ => AddNew());
        EditItemCommand = new RelayCommand(p => BeginEdit(p as Shortcut));
        SaveItemCommand = new RelayCommand(p => SaveItem(p as Shortcut));
        DeleteItemCommand = new RelayCommand(p => DeleteItem(p as Shortcut));
        CancelItemCommand = new RelayCommand(p => CancelEdit(p as Shortcut));
        MoveUpCommand = new RelayCommand(p => Move(p as Shortcut, -1));
        MoveDownCommand = new RelayCommand(p => Move(p as Shortcut, +1));
        ToggleManageCommand = new RelayCommand(_ => IsManageMode = !IsManageMode);
        SetCategoryCommand = new RelayCommand(SetCategoryOnEditing);

        Reload();
    }

    public void Reload()
    {
        _all.Clear();
        _all.AddRange(_storage.LoadShortcuts()); // 保存順を維持
        RebuildCategories();
        RefreshView();
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    private void RefreshView()
    {
        Items.Clear();
        foreach (var s in _all)
        {
            if (s.IsEditing || ShortcutSearch.Matches(s, SearchText, SelectedCategory))
                Items.Add(s);
        }
    }

    private void RebuildCategories()
    {
        var cats = ShortcutSearch.BuildCategoryList(_all);
        var current = SelectedCategory;
        Categories.Clear();
        foreach (var c in cats) Categories.Add(c);
        _selectedCategory = Categories.Contains(current) ? current : ShortcutSearch.AllCategories;
        OnPropertyChanged(nameof(SelectedCategory));
    }

    private void EndAllEdits()
    {
        foreach (var s in _all.Where(x => x.IsEditing).ToList())
        {
            if (s.IsNew) _all.Remove(s);
            else s.IsEditing = false;
        }
    }

    private void AddNew()
    {
        EndAllEdits();
        SearchText = string.Empty;
        SelectedCategory = ShortcutSearch.AllCategories;

        var s = new Shortcut { Category = "自作", IsNew = true, IsEditing = true };
        _all.Insert(0, s);
        SelectedShortcut = s;
        RefreshView();
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    private void BeginEdit(Shortcut? s)
    {
        if (s is null) return;
        EndAllEdits();
        s.IsEditing = true;
        SelectedShortcut = s;
        RefreshView();
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    private void SaveItem(Shortcut? s)
    {
        if (s is null) return;
        if (string.IsNullOrWhiteSpace(s.Keys) && string.IsNullOrWhiteSpace(s.Name))
        {
            CancelEdit(s);
            return;
        }
        s.IsEditing = false;
        s.IsNew = false;
        Persist();
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    private void DeleteItem(Shortcut? s)
    {
        if (s is null) return;
        _all.Remove(s);
        Persist();
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    private void CancelEdit(Shortcut? s)
    {
        if (s is null) return;
        if (s.IsNew)
        {
            _all.Remove(s);
            RebuildCategories();
            RefreshView();
        }
        else
        {
            s.IsEditing = false;
            Reload(); // 元に戻す
        }
        OnPropertyChanged(nameof(IsAnyEditing));
    }

    private void Move(Shortcut? s, int direction)
    {
        if (s is null) return;
        int index = _all.IndexOf(s);
        if (index < 0) return;
        int target = index + direction;
        if (target < 0 || target >= _all.Count) return;

        (_all[index], _all[target]) = (_all[target], _all[index]);
        SelectedShortcut = s;
        Persist();
    }

    private void SetCategoryOnEditing(object? param)
    {
        if (param is string cat && SelectedShortcut is { IsEditing: true } s)
            s.Category = cat;
    }

    private void Persist()
    {
        try
        {
            _storage.SaveShortcuts(_all);
            RebuildCategories();
            RefreshView();
        }
        catch (Exception ex)
        {
            _log.Error("一覧の保存に失敗しました。", ex);
        }
    }
}

using System.Collections.ObjectModel;
using ShortcutBoard.Helpers;
using ShortcutBoard.Models;
using ShortcutBoard.Services;

namespace ShortcutBoard.ViewModels;

/// <summary>
/// 編集（管理）画面の ViewModel。ショートカットの追加・編集・削除を行う。
/// 作業用コピーを編集し、保存時にディスクへ書き込む。
/// </summary>
public class EditViewModel : ObservableObject
{
    private readonly JsonStorageService _storage;
    private readonly LogService _log = LogService.Instance;

    public ObservableCollection<Shortcut> Items { get; } = new();

    /// <summary>編集候補のカテゴリー（コンボの候補に使用）。</summary>
    public ObservableCollection<string> CategorySuggestions { get; } = new()
    {
        "Windows", "Chrome", "PowerToys", "Claude", "ChatGPT", "自作",
    };

    private Shortcut? _selectedItem;
    public Shortcut? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedItem is not null;

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public RelayCommand AddCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand SaveCommand { get; }

    /// <summary>保存完了後に通知（MainViewModel の再読込に使用）。</summary>
    public event Action? Saved;

    public EditViewModel(JsonStorageService storage)
    {
        _storage = storage;

        AddCommand = new RelayCommand(_ => AddNew());
        DeleteCommand = new RelayCommand(_ => DeleteSelected(), _ => HasSelection);
        SaveCommand = new RelayCommand(_ => Save());

        LoadWorkingCopy();
    }

    private void LoadWorkingCopy()
    {
        foreach (var existing in Items)
            existing.PropertyChanged -= OnItemChanged;

        Items.Clear();
        foreach (var s in _storage.LoadShortcuts())
        {
            var copy = Clone(s);
            copy.PropertyChanged += OnItemChanged;
            Items.Add(copy);
        }
        IsDirty = false;
    }

    private void OnItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => MarkDirty();

    public void MarkDirty() => IsDirty = true;

    private void AddNew()
    {
        var item = new Shortcut
        {
            Keys = "",
            Name = "新しいショートカット",
            Description = "",
            Category = "自作",
        };
        item.PropertyChanged += OnItemChanged;
        Items.Add(item);
        SelectedItem = item;
        MarkDirty();
    }

    private void DeleteSelected()
    {
        if (SelectedItem is null) return;
        Items.Remove(SelectedItem);
        SelectedItem = Items.FirstOrDefault();
        MarkDirty();
    }

    public void Save()
    {
        try
        {
            _storage.SaveShortcuts(Items);
            IsDirty = false;
            _log.Info($"ショートカットを保存しました（{Items.Count} 件）。");
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            _log.Error("保存に失敗しました。", ex);
            throw;
        }
    }

    private static Shortcut Clone(Shortcut s) => new()
    {
        Id = s.Id,
        Keys = s.Keys,
        Name = s.Name,
        Description = s.Description,
        Category = s.Category,
    };
}

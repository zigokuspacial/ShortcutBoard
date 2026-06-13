using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace ShortcutBoard.Models;

/// <summary>
/// 1件のショートカット情報を表すモデル。
/// 編集画面でのライブ更新のため INotifyPropertyChanged を実装する。
/// （Windows/macOS 共通）
/// </summary>
public class Shortcut : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _keys = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _category = string.Empty;

    /// <summary>一意なID（編集・削除の識別に使用）。</summary>
    public string Id
    {
        get => _id;
        set => Set(ref _id, value);
    }

    /// <summary>ショートカットキー（例: "Win + Ctrl + D"）。</summary>
    public string Keys
    {
        get => _keys;
        set => Set(ref _keys, value);
    }

    /// <summary>名前。</summary>
    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    /// <summary>説明。</summary>
    public string Description
    {
        get => _description;
        set => Set(ref _description, value);
    }

    /// <summary>カテゴリー（例: Windows / Chrome / PowerToys など）。</summary>
    public string Category
    {
        get => _category;
        set { if (Set(ref _category, value)) OnPropertyChanged(nameof(DisplayCategory)); }
    }

    [JsonIgnore]
    public string DisplayCategory => string.IsNullOrWhiteSpace(Category) ? "未分類" : Category;

    private bool _isEditing;
    /// <summary>一覧内でインライン編集中かどうか（保存しない）。</summary>
    [JsonIgnore]
    public bool IsEditing
    {
        get => _isEditing;
        set => Set(ref _isEditing, value);
    }

    private bool _isNew;
    /// <summary>まだ一度も保存されていない新規行か（キャンセル時に削除する）。</summary>
    [JsonIgnore]
    public bool IsNew
    {
        get => _isNew;
        set => Set(ref _isNew, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using ShortcutBoard.Models;
using ShortcutBoard.ViewModels;

namespace ShortcutBoard.Views;

/// <summary>
/// ショートカットの追加・編集・削除を行う管理画面。
/// 通常のウィンドウとして扱う（メイン一覧の自動非表示対象ではない）。
/// </summary>
public partial class EditWindow : Window
{
    private readonly EditViewModel _vm;

    public EditWindow(EditViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void CategorySuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem is Shortcut s && sender is Button b && b.Tag is string cat)
            s.Category = cat;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        TrySave();
    }

    private bool TrySave()
    {
        try
        {
            _vm.Save();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"保存に失敗しました。\n{ex.Message}",
                "ShortcutBoard", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        // 未保存の変更があれば確認する
        if (_vm.IsDirty)
        {
            var result = MessageBox.Show(this,
                "未保存の変更があります。保存しますか？",
                "ShortcutBoard",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    if (!TrySave()) e.Cancel = true; // 保存失敗時は閉じない
                    break;
                case MessageBoxResult.No:
                    break; // 破棄して閉じる
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    break;
            }
        }
        base.OnClosing(e);
    }
}

using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ShortcutBoard.Models;
using ShortcutBoard.Services;
using ShortcutBoard.ViewModels;

namespace ShortcutBoard.Views;

/// <summary>
/// メイン一覧ウィンドウ。閉じる代わりに Hide し、トレイに常駐する。
/// 一覧内で直接インライン追加・編集ができる。
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly LogService _log = LogService.Instance;

    /// <summary>0 より大きい間は Deactivated による自動非表示を抑制する。</summary>
    private int _suppressAutoHide;

    private bool _editSuppressActive;

    // フェードイン／アウト用
    private bool _isAnimatingOut;
    private static readonly TimeSpan InDuration = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan OutDuration = TimeSpan.FromMilliseconds(160);

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        if (_vm.ShortcutsView is INotifyCollectionChanged incc)
            incc.CollectionChanged += (_, _) => UpdateCount();

        // インライン編集中は自動非表示を抑制する
        _vm.PropertyChanged += OnVmPropertyChanged;

        // コンボ操作中は自動非表示を抑制
        CategoryCombo.DropDownOpened += (_, _) => PushSuppressAutoHide();
        CategoryCombo.DropDownClosed += (_, _) => PopSuppressAutoHide();

        Deactivated += OnDeactivated;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsAnyEditing)) return;

        // 編集状態に合わせて抑制カウンタを増減（多重にならないよう管理）
        if (_vm.IsAnyEditing && !_editSuppressActive)
        {
            _editSuppressActive = true;
            PushSuppressAutoHide();
        }
        else if (!_vm.IsAnyEditing && _editSuppressActive)
        {
            _editSuppressActive = false;
            PopSuppressAutoHide();
        }
    }

    // ---------------- 表示・非表示 ----------------

    public void ShowAtCenter()
    {
        // 消えるアニメーション中なら中断
        if (_isAnimatingOut)
        {
            BeginAnimation(OpacityProperty, null);
            _isAnimatingOut = false;
        }

        bool needFade = !IsVisible || Opacity < 1.0;

        PositionToActiveScreenCenter();
        if (needFade) Opacity = 0;

        Show();
        Visibility = Visibility.Visible;
        Topmost = true;
        Activate();
        SearchBox.Focus();
        SearchBox.SelectAll();
        UpdateCount();

        if (needFade) AnimateIn();
    }

    /// <summary>フェードインのみ（不透明度 0→1）。</summary>
    private void AnimateIn()
    {
        var fade = new DoubleAnimation(0, 1, InDuration);
        fade.Completed += (_, _) =>
        {
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;
        };
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>フェードアウトのみ（不透明度 →0）してから Hide。アプリは終了しない。</summary>
    public void HideBoard()
    {
        if (!IsVisible || _isAnimatingOut) return;
        _isAnimatingOut = true;

        var fade = new DoubleAnimation(Opacity, 0, OutDuration);
        fade.Completed += (_, _) =>
        {
            _isAnimatingOut = false;
            Hide();
            BeginAnimation(OpacityProperty, null);
            Opacity = 1;                 // 次回表示に備えて戻す
        };
        BeginAnimation(OpacityProperty, fade);
    }

    private void PositionToActiveScreenCenter()
    {
        try
        {
            var pos = System.Windows.Forms.Cursor.Position;
            var screen = System.Windows.Forms.Screen.FromPoint(pos);
            var area = screen.WorkingArea;

            var src = PresentationSource.FromVisual(this);
            double dpiX = 1.0, dpiY = 1.0;
            if (src?.CompositionTarget is not null)
            {
                dpiX = src.CompositionTarget.TransformToDevice.M11;
                dpiY = src.CompositionTarget.TransformToDevice.M22;
            }

            double areaLeft = area.Left / dpiX;
            double areaTop = area.Top / dpiY;
            double areaWidth = area.Width / dpiX;
            double areaHeight = area.Height / dpiY;

            Left = areaLeft + (areaWidth - Width) / 2;
            Top = areaTop + (areaHeight - Height) / 2;
        }
        catch (Exception ex)
        {
            _log.Error("ウィンドウ位置の計算に失敗。中央へフォールバック。", ex);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    // ---------------- 自動非表示の抑制 ----------------

    public void PushSuppressAutoHide() => _suppressAutoHide++;

    public void PopSuppressAutoHide()
    {
        if (_suppressAutoHide > 0) _suppressAutoHide--;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_suppressAutoHide > 0) return;
        if (!IsVisible) return;
        HideBoard();
    }

    // ---------------- キーボード操作 ----------------

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // インライン編集中は一覧ショートカットを無効化（テキスト入力を優先）
        if (_vm.IsAnyEditing)
        {
            if (e.Key == Key.Escape)
            {
                _vm.CancelItemCommand.Execute(_vm.SelectedShortcut);
                e.Handled = true;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.Escape:
                HideBoard();
                e.Handled = true;
                break;

            case Key.Enter:
                CopySelectedKeys();
                e.Handled = true;
                break;

            case Key.Down:
            case Key.Up:
                if (Keyboard.FocusedElement is TextBox)
                {
                    MoveSelection(e.Key == Key.Down ? 1 : -1);
                    e.Handled = true;
                }
                break;
        }
    }

    private void MoveSelection(int delta)
    {
        if (List.Items.Count == 0) return;

        int index = List.SelectedIndex + delta;
        index = Math.Max(0, Math.Min(List.Items.Count - 1, index));
        List.SelectedIndex = index;
        List.ScrollIntoView(List.SelectedItem);

        if (List.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item)
            item.Focus();
    }

    private void List_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 管理モードのみ、表示中の行をダブルクリックで編集開始。
        if (!_vm.IsManageMode || _vm.IsAnyEditing) return;
        if (_vm.SelectedShortcut is Shortcut s)
            _vm.EditItemCommand.Execute(s);
    }

    private DispatcherTimer? _toastTimer;

    private void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not Shortcut s) return;

        // 確認ダイアログなしで即削除・即保存（VM 側で保存まで実施）
        _vm.DeleteItemCommand.Execute(s);

        // 軽い通知（「削除しました／元に戻す」）を一定時間表示
        ShowDeleteToast();
    }

    private void ShowDeleteToast()
    {
        DeleteToast.Visibility = Visibility.Visible;

        _toastTimer ??= new DispatcherTimer();
        _toastTimer.Stop();
        _toastTimer.Interval = TimeSpan.FromSeconds(5);
        _toastTimer.Tick -= ToastTimer_Tick;
        _toastTimer.Tick += ToastTimer_Tick;
        _toastTimer.Start();
    }

    private void ToastTimer_Tick(object? sender, EventArgs e)
    {
        _toastTimer?.Stop();
        DeleteToast.Visibility = Visibility.Collapsed;
    }

    private void UndoDelete_Click(object sender, RoutedEventArgs e)
    {
        _vm.UndoLastDelete();
        _toastTimer?.Stop();
        DeleteToast.Visibility = Visibility.Collapsed;
    }

    private void CopySelectedKeys()
    {
        if (_vm.SelectedShortcut is not Shortcut s || string.IsNullOrEmpty(s.Keys))
            return;
        try
        {
            Clipboard.SetText(s.Keys);
            CountText.Text = $"「{s.Keys}」をコピーしました";
        }
        catch (Exception ex)
        {
            _log.Error("クリップボードへのコピーに失敗。", ex);
        }
    }

    private void UpdateCount()
    {
        CountText.Text = $"{_vm.ShortcutsView.Cast<object>().Count()} 件";
    }

    // ---------------- 閉じる挙動 ----------------

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isReallyClosing)
        {
            e.Cancel = true;
            HideBoard();
        }
        base.OnClosing(e);
    }

    private bool _isReallyClosing;

    public void AllowClose() => _isReallyClosing = true;
}

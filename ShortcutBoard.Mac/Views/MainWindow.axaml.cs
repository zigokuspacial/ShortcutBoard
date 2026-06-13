using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ShortcutBoard.Mac.ViewModels;

namespace ShortcutBoard.Mac.Views;

public partial class MainWindow : Window
{
    /// <summary>true の間は外側クリック等による自動非表示を抑制（設定画面表示中など）。</summary>
    public bool SuppressHide { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        Deactivated += OnDeactivated;
        KeyDown += OnKeyDown;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private MacMainViewModel? Vm => DataContext as MacMainViewModel;

    /// <summary>画面中央に表示して最前面化する。</summary>
    public void ShowCentered()
    {
        if (!IsVisible)
        {
            var screen = Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null);
            if (screen is not null)
            {
                var wa = screen.WorkingArea;
                Position = new PixelPoint(
                    wa.X + (wa.Width - (int)Width) / 2,
                    wa.Y + (wa.Height - (int)Height) / 2);
            }
            Show();
        }
        Activate();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (SuppressHide) return;
        // 編集中は閉じない（入力中に消えるのを防ぐ）
        if (Vm?.IsAnyEditing == true) return;
        if (IsVisible) Hide();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        if (Vm?.IsAnyEditing == true)
        {
            Vm.CancelItemCommand.Execute(Vm.SelectedShortcut);
        }
        else
        {
            Hide();
        }
        e.Handled = true;
    }
}

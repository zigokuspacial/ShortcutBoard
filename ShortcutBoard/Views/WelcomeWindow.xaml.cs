using System.Windows;

namespace ShortcutBoard.Views;

/// <summary>
/// 初回起動時のオンボーディングポップアップ。
/// 表示制御（初回のみ）は App 側の HasShownWelcome フラグで行う。
/// </summary>
public partial class WelcomeWindow : Window
{
    public WelcomeWindow()
    {
        InitializeComponent();
    }

    private void Start_Click(object sender, RoutedEventArgs e) => Close();
}

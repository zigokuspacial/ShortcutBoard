using Avalonia;

namespace ShortcutBoard.Mac;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // CI 用: UI を起動せずに共通ロジックの読み書きを確認して終了する
        if (args.Contains("--selftest"))
            return SelfTest.Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

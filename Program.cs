using Avalonia;

using System;
using System.Linq;

namespace WebLoadTester;

/// <summary>
/// Точка входа приложения: настраивает и запускает Avalonia.
/// </summary>
sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    /// <summary>
    /// Запускает приложение с классическим жизненным циклом окна.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--smoke-report", StringComparison.OrdinalIgnoreCase)))
        {
            return SmokeReportRunner.RunAsync().GetAwaiter().GetResult();
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    /// <summary>
    /// Собирает конфигурацию Avalonia для запуска и дизайнера.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}

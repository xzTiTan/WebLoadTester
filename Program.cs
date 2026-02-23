using Avalonia;
using System;
using System.IO;
using WebLoadTester.Infrastructure.Playwright;

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
    public static void Main(string[] args)
    {
        var browsersPath = Path.Combine(AppContext.BaseDirectory, "playwright-browsers");
        Directory.CreateDirectory(browsersPath);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);
        PlaywrightFactory.ConfigureBrowsersPath(browsersPath);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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

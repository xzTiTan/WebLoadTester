using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace WebLoadTester.Infrastructure.Playwright;

/// <summary>
/// Фабрика Playwright с локальным каталогом браузеров.
/// </summary>
public static class PlaywrightFactory
{
    /// <summary>
    /// Создаёт экземпляр Playwright и задаёт путь к браузерам.
    /// </summary>
    public static async Task<IPlaywright> CreateAsync()
    {
        var browsersPath = Path.Combine(AppContext.BaseDirectory, "browsers");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);
        return await Microsoft.Playwright.Playwright.CreateAsync();
    }

    /// <summary>
    /// Проверяет наличие установленных браузеров для Playwright.
    /// </summary>
    public static bool HasBrowsersInstalled()
    {
        var browsersPath = Path.Combine(AppContext.BaseDirectory, "browsers");
        return Directory.Exists(browsersPath) && Directory.GetDirectories(browsersPath).Length > 0;
    }
}

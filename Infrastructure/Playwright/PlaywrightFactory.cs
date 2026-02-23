using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace WebLoadTester.Infrastructure.Playwright;

/// <summary>
/// Фабрика Playwright с локальным каталогом браузеров.
/// </summary>
public static class PlaywrightFactory
{
    private static string _browsersPath = Path.Combine(AppContext.BaseDirectory, "playwright-browsers");

    public static string BrowsersPath => _browsersPath;

    public static void ConfigureBrowsersPath(string browsersPath)
    {
        if (string.IsNullOrWhiteSpace(browsersPath))
        {
            return;
        }

        _browsersPath = browsersPath;
        Directory.CreateDirectory(_browsersPath);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", _browsersPath);
    }

    /// <summary>
    /// Создаёт экземпляр Playwright и задаёт путь к браузерам.
    /// </summary>
    public static async Task<IPlaywright> CreateAsync()
    {
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", BrowsersPath);
        return await Microsoft.Playwright.Playwright.CreateAsync();
    }

    /// <summary>
    /// Проверяет наличие установленных браузеров для Playwright.
    /// </summary>
    public static bool HasBrowsersInstalled()
    {
        if (!Directory.Exists(BrowsersPath))
        {
            return false;
        }

        foreach (var directory in Directory.GetDirectories(BrowsersPath))
        {
            var name = Path.GetFileName(directory).ToLowerInvariant();
            if (!name.StartsWith("chromium-", StringComparison.Ordinal) &&
                !name.Contains("chromium", StringComparison.Ordinal))
            {
                continue;
            }

            if (File.Exists(Path.Combine(directory, "chrome-linux", "chrome")) ||
                File.Exists(Path.Combine(directory, "chrome-win", "chrome.exe")) ||
                File.Exists(Path.Combine(directory, "chrome-mac", "Chromium.app", "Contents", "MacOS", "Chromium")))
            {
                return true;
            }

            if (Directory.EnumerateFiles(directory, "*chrome*", SearchOption.AllDirectories).Any())
            {
                return true;
            }
        }

        return false;
    }

    public static string GetBrowsersPath() => BrowsersPath;

    public static async Task InstallChromiumAsync(CancellationToken ct, Action<string>? onOutput = null)
    {
        Directory.CreateDirectory(BrowsersPath);
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", BrowsersPath);
        onOutput?.Invoke($"PLAYWRIGHT_BROWSERS_PATH={BrowsersPath}");
        onOutput?.Invoke("Installing chromium via Microsoft.Playwright.Program...");

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Playwright install failed with code {exitCode}.");
            }
        }, ct);

        onOutput?.Invoke("Chromium install completed.");
    }
}

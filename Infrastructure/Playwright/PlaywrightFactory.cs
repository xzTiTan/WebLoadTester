using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace WebLoadTester.Infrastructure.Playwright;

/// <summary>
/// Фабрика Playwright с локальным каталогом браузеров.
/// </summary>
public static class PlaywrightFactory
{
    private static string BrowsersPath => Path.Combine(AppContext.BaseDirectory, "browsers");

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
        return Directory.Exists(BrowsersPath) && Directory.GetDirectories(BrowsersPath).Length > 0;
    }

    public static string GetBrowsersPath() => BrowsersPath;

    public static async Task InstallChromiumAsync(CancellationToken ct)
    {
        var script = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "playwright.ps1" : "playwright.sh";
        var scriptPath = Path.Combine(AppContext.BaseDirectory, script);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Playwright installer script not found: {scriptPath}");
        }

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "powershell" : "bash",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false
        };
        psi.ArgumentList.Add(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "-ExecutionPolicy" : scriptPath);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(scriptPath);
        }
        psi.ArgumentList.Add("install");
        psi.ArgumentList.Add("chromium");
        psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] = BrowsersPath;

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Playwright install failed with code {process.ExitCode}.");
        }
    }
}

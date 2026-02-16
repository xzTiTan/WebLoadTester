using System;
using System.Diagnostics;
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
    private static string _browsersPath = Path.Combine(AppContext.BaseDirectory, "browsers");

    public static string BrowsersPath => _browsersPath;

    public static void ConfigureBrowsersPath(string browsersPath)
    {
        if (string.IsNullOrWhiteSpace(browsersPath))
        {
            return;
        }

        _browsersPath = browsersPath;
        Directory.CreateDirectory(_browsersPath);
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
            if (name.Contains("chrom"))
            {
                return true;
            }
        }

        return false;
    }

    public static string GetBrowsersPath() => BrowsersPath;

    public static async Task InstallChromiumAsync(CancellationToken ct, Action<string>? onOutput = null)
    {
        var script = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "playwright.ps1" : "playwright.sh";
        var scriptPath = Path.Combine(AppContext.BaseDirectory, script);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Playwright installer script not found: {scriptPath}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pwsh" : "bash",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi.ArgumentList.Add(scriptPath);
        }
        else
        {
            psi.ArgumentList.Add(scriptPath);
        }

        psi.ArgumentList.Add("install");
        psi.ArgumentList.Add("chromium");
        psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] = BrowsersPath;

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                onOutput?.Invoke(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                onOutput?.Invoke(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Playwright install failed with code {process.ExitCode}.");
        }
    }
}

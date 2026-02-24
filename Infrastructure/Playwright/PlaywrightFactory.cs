using System;
using System.Diagnostics;
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
    private static string _browsersPath = GetDefaultBrowsersPath();
    private static int _isInstalling;

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
        return HasChromiumInPath(BrowsersPath);
    }

    public static string GetBrowsersPath() => BrowsersPath;

    public static bool IsInstalling => Volatile.Read(ref _isInstalling) == 1;

    public static async Task<bool> InstallChromiumAsync(CancellationToken ct, Action<string>? onOutput = null)
    {
        if (Interlocked.CompareExchange(ref _isInstalling, 1, 0) != 0)
        {
            onOutput?.Invoke("Chromium installation is already in progress.");
            return false;
        }

        try
        {
            ConfigureBrowsersPath(BrowsersPath);

            onOutput?.Invoke($"PLAYWRIGHT_BROWSERS_PATH={BrowsersPath}");
            onOutput?.Invoke("Starting Playwright chromium install...");

            var embeddedInstallSucceeded = false;
            try
            {
                onOutput?.Invoke("Running embedded Microsoft.Playwright.Program.Main(install chromium)...");
                var exitCode = await Task.Run(() => Microsoft.Playwright.Program.Main(new[] { "install", "chromium" }), ct);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"Playwright install failed with code {exitCode}.");
                }

                embeddedInstallSucceeded = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                onOutput?.Invoke($"Embedded install failed, switching to process fallback: {ex}");
            }

            if (!embeddedInstallSucceeded)
            {
                await RunInstallFallbackProcessAsync(ct, onOutput);
            }

            var hasBrowsers = HasBrowsersInstalled();
            onOutput?.Invoke(hasBrowsers
                ? "Chromium install completed and detected."
                : "Chromium install command finished, but chromium marker was not detected.");

            return hasBrowsers;
        }
        finally
        {
            Interlocked.Exchange(ref _isInstalling, 0);
        }
    }

    public static bool HasLegacyBaseDirectoryBrowsersInstall()
    {
        var legacyPath = Path.Combine(AppContext.BaseDirectory, "playwright-browsers");
        if (Path.GetFullPath(legacyPath).Equals(Path.GetFullPath(BrowsersPath), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HasChromiumInPath(legacyPath);
    }

    private static string GetDefaultBrowsersPath()
    {
        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebLoadTester", "data");
        return Path.Combine(dataDirectory, "browsers");
    }

    private static bool HasChromiumInPath(string browsersPath)
    {
        if (!Directory.Exists(browsersPath))
        {
            return false;
        }

        foreach (var directory in Directory.GetDirectories(browsersPath))
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

    private static async Task RunInstallFallbackProcessAsync(CancellationToken ct, Action<string>? onOutput)
    {
        var baseDir = AppContext.BaseDirectory;
        var playwrightDll = Path.Combine(baseDir, "Microsoft.Playwright.CLI.dll");

        if (File.Exists(playwrightDll))
        {
            onOutput?.Invoke($"Fallback: dotnet {Path.GetFileName(playwrightDll)} install chromium");
            await RunProcessAsync("dotnet", $"\"{playwrightDll}\" install chromium", ct, onOutput);
            return;
        }

        var playwrightScript = FindPlaywrightScript(baseDir);
        if (!string.IsNullOrWhiteSpace(playwrightScript))
        {
            onOutput?.Invoke($"Fallback: running script {playwrightScript}");
            if (OperatingSystem.IsWindows())
            {
                var shell = playwrightScript.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ? "powershell" : "cmd";
                var args = shell == "powershell"
                    ? $"-NoProfile -ExecutionPolicy Bypass -File \"{playwrightScript}\" install chromium"
                    : $"/c \"\"{playwrightScript}\" install chromium\"";
                await RunProcessAsync(shell, args, ct, onOutput);
            }
            else
            {
                await RunProcessAsync("bash", $"\"{playwrightScript}\" install chromium", ct, onOutput);
            }

            return;
        }

        throw new FileNotFoundException("Playwright CLI fallback not found (Microsoft.Playwright.CLI.dll/playwright script).");
    }

    private static async Task RunProcessAsync(string fileName, string arguments, CancellationToken ct, Action<string>? onOutput)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

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
                onOutput?.Invoke($"ERR: {e.Data}");
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {fileName} {arguments}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Process install failed with code {process.ExitCode}: {fileName} {arguments}");
        }
    }

    private static string? FindPlaywrightScript(string baseDir)
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { "playwright.cmd", "playwright.ps1" }
            : new[] { "playwright.sh" };

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(baseDir, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}

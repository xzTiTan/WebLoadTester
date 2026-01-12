using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.UiSnapshot;

/// <summary>
/// Модуль массового снятия скриншотов UI.
/// </summary>
public class UiSnapshotModule : ITestModule
{
    public string Id => "ui.snapshot";
    public string DisplayName => "UI снимки";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiSnapshotSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию.
    /// </summary>
    public object CreateDefaultSettings()
    {
        return new UiSnapshotSettings
        {
            Paths = new List<string> { "/" }
        };
    }

    /// <summary>
    /// Проверяет корректность параметров снимков.
    /// </summary>
    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not UiSnapshotSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (s.Paths.Count == 0)
        {
            errors.Add("At least one URL is required");
        }

        if (!Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("BaseUrl is required");
        }

        if (s.Paths.Any(path => string.IsNullOrWhiteSpace(path)))
        {
            errors.Add("Path is required");
        }

        if (s.Concurrency <= 0)
        {
            errors.Add("Concurrency must be positive");
        }

        return errors;
    }

    /// <summary>
    /// Делает скриншоты указанных URL и формирует отчёт.
    /// </summary>
    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiSnapshotSettings)settings;
        var report = new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Completed,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            AppVersion = typeof(UiSnapshotModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            ctx.Log.Error("Playwright browsers not found. Install browsers into ./browsers.");
            report.Status = TestStatus.Error;
            report.Results.Add(new RunResult("Playwright")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = "Playwright",
                ErrorMessage = "Install browsers"
            });
            report.FinishedAt = ctx.Now;
            return report;
        }

        using var playwright = await PlaywrightFactory.CreateAsync();
        var waitUntil = s.WaitMode == "domcontentloaded" ? WaitUntilState.DOMContentLoaded : WaitUntilState.Load;
        var semaphore = new SemaphoreSlim(Math.Min(s.Concurrency, ctx.Limits.MaxUiConcurrency));
        var results = new List<ResultBase>();
        var completed = 0;
        var runFolder = ctx.Artifacts.CreateRunFolder(report.StartedAt.ToString("yyyyMMdd_HHmmss"));

        var urls = s.Paths.Select(path => ResolveUrl(s.BaseUrl, path)).ToList();
        var tasks = urls.Select(async url =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                    var page = await browser.NewPageAsync();
                    await page.GotoAsync(url, new PageGotoOptions { WaitUntil = waitUntil });
                    if (s.DelayAfterLoadMs > 0)
                    {
                        await Task.Delay(s.DelayAfterLoadMs, ct);
                    }
                    var bytes = await page.ScreenshotAsync();
                    var fileName = $"snapshot_{Sanitize(url)}.png";
                    var path = await ctx.Artifacts.SaveScreenshotAsync(bytes, runFolder, fileName);
                    sw.Stop();
                    results.Add(new RunResult(url)
                    {
                        Success = true,
                        DurationMs = sw.Elapsed.TotalMilliseconds,
                        ScreenshotPath = path
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results.Add(new RunResult(url)
                    {
                        Success = false,
                        DurationMs = sw.Elapsed.TotalMilliseconds,
                        ErrorType = ex.GetType().Name,
                        ErrorMessage = ex.Message
                    });
                }
                finally
                {
                    var done = Interlocked.Increment(ref completed);
                    ctx.Progress.Report(new ProgressUpdate(done, urls.Count, "UI снимки"));
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        report.Results = results;
        report.FinishedAt = ctx.Now;
        return report;
    }

    /// <summary>
    /// Превращает URL в безопасное имя файла.
    /// </summary>
    private static string Sanitize(string url)
    {
        foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
        {
            url = url.Replace(ch, '_');
        }
        return url.Replace("https://", string.Empty).Replace("http://", string.Empty).Replace("/", "_");
    }

    private static string ResolveUrl(string baseUrl, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        var root = new Uri(baseUrl, UriKind.Absolute);
        return new Uri(root, path).ToString();
    }
}

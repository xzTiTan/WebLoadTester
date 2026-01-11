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

namespace WebLoadTester.Modules.UiTiming;

/// <summary>
/// Модуль замеров времени загрузки страниц через Playwright.
/// </summary>
public class UiTimingModule : ITestModule
{
    public string Id => "ui.timing";
    public string DisplayName => "UI тайминги";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiTimingSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию.
    /// </summary>
    public object CreateDefaultSettings()
    {
        return new UiTimingSettings
        {
            Urls = new List<string> { "https://example.com" }
        };
    }

    /// <summary>
    /// Проверяет корректность настроек замеров.
    /// </summary>
    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not UiTimingSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (s.Urls.Count == 0)
        {
            errors.Add("At least one URL is required");
        }

        if (s.RepeatsPerUrl <= 0)
        {
            errors.Add("RepeatsPerUrl must be positive");
        }

        return errors;
    }

    /// <summary>
    /// Выполняет замеры времени и формирует отчёт.
    /// </summary>
    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiTimingSettings)settings;
        var report = new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Completed,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            AppVersion = typeof(UiTimingModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
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
        var waitUntil = s.WaitUntil == "domcontentloaded" ? WaitUntilState.DOMContentLoaded : WaitUntilState.Load;
        var semaphore = new SemaphoreSlim(Math.Min(s.Concurrency, ctx.Limits.MaxUiConcurrency));
        var results = new List<ResultBase>();
        var total = s.Urls.Count * s.RepeatsPerUrl;
        var completed = 0;

        var tasks = s.Urls.SelectMany(url => Enumerable.Range(1, s.RepeatsPerUrl).Select(iteration => (url, iteration)))
            .Select(async item =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
                        var page = await browser.NewPageAsync();
                        await page.GotoAsync(item.url, new PageGotoOptions { WaitUntil = waitUntil });
                        sw.Stop();
                        results.Add(new TimingResult($"{item.url}")
                        {
                            Url = item.url,
                            Iteration = item.iteration,
                            Success = true,
                            DurationMs = sw.Elapsed.TotalMilliseconds
                        });
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        results.Add(new TimingResult($"{item.url}")
                        {
                            Url = item.url,
                            Iteration = item.iteration,
                            Success = false,
                            DurationMs = sw.Elapsed.TotalMilliseconds,
                            ErrorType = ex.GetType().Name,
                            ErrorMessage = ex.Message
                        });
                    }
                    finally
                    {
                        var done = Interlocked.Increment(ref completed);
                        ctx.Progress.Report(new ProgressUpdate(done, total, "UI тайминги"));
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
}

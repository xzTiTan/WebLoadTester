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
    public string Description => "Измеряет скорость загрузки UI и вычисляет агрегаты (avg, p95/p99 при достаточном N).";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiTimingSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию.
    /// </summary>
    public object CreateDefaultSettings()
    {
        return new UiTimingSettings
        {
            Targets = new List<TimingTarget>
            {
                new() { Url = "https://example.com", Tag = "Example" }
            }
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

        if (s.Targets.Count == 0)
        {
            errors.Add("At least one URL is required");
        }

        if (s.Targets.Any(target => !Uri.TryCreate(target.Url, UriKind.Absolute, out _)))
        {
            errors.Add("Each URL must be absolute");
        }

        if (s.RepeatsPerUrl <= 0)
        {
            errors.Add("RepeatsPerUrl must be positive");
        }

        if (s.TimeoutMs <= 0)
        {
            errors.Add("Timeout must be positive");
        }

        return errors;
    }

    /// <summary>
    /// Выполняет замеры времени и формирует результат.
    /// </summary>
    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiTimingSettings)settings;
        var result = new ModuleResult();

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            ctx.Log.Error("Playwright browsers not found. Install browsers into ./browsers.");
            result.Status = TestStatus.Failed;
            result.Results.Add(new RunResult("Playwright")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = "Playwright",
                ErrorMessage = "Install browsers"
            });
            return result;
        }

        using var playwright = await PlaywrightFactory.CreateAsync();
        var waitUntil = s.WaitUntil switch
        {
            "domcontentloaded" => WaitUntilState.DOMContentLoaded,
            "networkidle" => WaitUntilState.NetworkIdle,
            _ => WaitUntilState.Load
        };
        var semaphore = new SemaphoreSlim(Math.Min(s.Concurrency, ctx.Limits.MaxUiConcurrency));
        var results = new List<ResultBase>();
        var runs = s.Targets.SelectMany(target =>
            Enumerable.Range(1, s.RepeatsPerUrl).Select(iteration => (target, iteration))).ToList();
        var total = runs.Count;
        var completed = 0;

        var tasks = runs.Select(async run =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = s.Headless });
                        var page = await browser.NewPageAsync();
                        await page.GotoAsync(run.target.Url, new PageGotoOptions
                        {
                            WaitUntil = waitUntil,
                            Timeout = s.TimeoutMs
                        });
                        sw.Stop();
                        results.Add(new TimingResult(run.target.Tag ?? run.target.Url)
                        {
                            Url = run.target.Url,
                            Iteration = run.iteration,
                            Success = true,
                            DurationMs = sw.Elapsed.TotalMilliseconds
                        });
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        results.Add(new TimingResult(run.target.Tag ?? run.target.Url)
                        {
                            Url = run.target.Url,
                            Iteration = run.iteration,
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
        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }
}

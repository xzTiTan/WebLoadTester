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
            },
            WaitUntil = "load",
            TimeoutSeconds = 30
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

        if (s.TimeoutSeconds <= 0)
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
            var browsersPath = PlaywrightFactory.GetBrowsersPath();
            ctx.Log.Error($"[UiTiming] Chromium browser not found. Install browsers into: {browsersPath}");
            result.Status = TestStatus.Failed;
            result.Results.Add(new RunResult("Playwright")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = "Playwright",
                ErrorMessage = $"Chromium is not installed. Run playwright install chromium (path: {browsersPath})."
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
        var results = new List<ResultBase>();
        ctx.Log.Info($"[UiTiming] Launching browser (Headless={ctx.Profile.Headless})");
        var total = s.Targets.Count;
        var completed = 0;

        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = ctx.Profile.Headless });
        var index = 0;
        foreach (var target in s.Targets)
        {
            var sw = Stopwatch.StartNew();
            index++;
            try
            {
                var page = await browser.NewPageAsync();
                await page.GotoAsync(target.Url, new PageGotoOptions
                {
                    WaitUntil = waitUntil,
                    Timeout = s.TimeoutSeconds * 1000
                });
                sw.Stop();
                results.Add(new TimingResult(target.Tag ?? target.Url)
                {
                    Url = target.Url,
                    Iteration = index,
                    Success = true,
                    DurationMs = sw.Elapsed.TotalMilliseconds
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new TimingResult(target.Tag ?? target.Url)
                {
                    Url = target.Url,
                    Iteration = index,
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
        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }
}

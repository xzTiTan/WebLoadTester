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

public sealed class UiTimingModule : ITestModule
{
    public string Id => "ui-timing";
    public string DisplayName => "UI Timing";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiTimingSettings);

    public object CreateDefaultSettings() => new UiTimingSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (UiTimingSettings)settings;
        var errors = new List<string>();
        if (s.Urls.Count == 0)
        {
            errors.Add("At least one URL is required.");
        }
        if (s.RepeatsPerUrl <= 0)
        {
            errors.Add("RepeatsPerUrl must be positive.");
        }
        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiTimingSettings)settings;
        var results = new List<TestResult>();
        var total = s.Urls.Count * s.RepeatsPerUrl;

        IPlaywright playwright;
        try
        {
            playwright = await PlaywrightFactory.CreateAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException ex)
        {
            ctx.Log.Error($"Playwright error: {ex.Message}. Install browsers via 'playwright install'.");
            throw;
        }

        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        }).ConfigureAwait(false);

        var semaphore = new SemaphoreSlim(Math.Min(s.Concurrency, ctx.Limits.MaxUiConcurrency));
        var completed = 0;
        var tasks = s.Urls.SelectMany(entry => Enumerable.Range(1, s.RepeatsPerUrl).Select(async iteration =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            var sw = Stopwatch.StartNew();
            try
            {
                await using var page = await browser.NewPageAsync().ConfigureAwait(false);
                var waitUntil = s.WaitUntil.Equals("domcontentloaded", StringComparison.OrdinalIgnoreCase)
                    ? WaitUntilState.DOMContentLoaded
                    : WaitUntilState.Load;
                await page.GotoAsync(entry.Url, new PageGotoOptions { WaitUntil = waitUntil }).ConfigureAwait(false);
                sw.Stop();
                lock (results)
                {
                    results.Add(new RunResult($"{entry.Url} #{iteration}", true, null, sw.Elapsed.TotalMilliseconds, null));
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                lock (results)
                {
                    results.Add(new RunResult($"{entry.Url} #{iteration}", false, ex.Message, sw.Elapsed.TotalMilliseconds, null));
                }
            }
            finally
            {
                var current = Interlocked.Increment(ref completed);
                ctx.Progress.Report(new ProgressUpdate(current, total));
                semaphore.Release();
            }
        }));

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            FinishedAt = ctx.Now,
            Status = TestStatus.Completed,
            Results = results
        };
    }
}

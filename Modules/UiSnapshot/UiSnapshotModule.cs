using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.UiSnapshot;

public sealed class UiSnapshotModule : ITestModule
{
    public string Id => "ui-snapshot";
    public string DisplayName => "UI Snapshot";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiSnapshotSettings);

    public object CreateDefaultSettings() => new UiSnapshotSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (UiSnapshotSettings)settings;
        var errors = new List<string>();
        if (s.Urls.Count == 0)
        {
            errors.Add("At least one URL is required.");
        }
        if (s.Concurrency <= 0)
        {
            errors.Add("Concurrency must be positive.");
        }
        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiSnapshotSettings)settings;
        var results = new List<TestResult>();
        var runFolder = ctx.Artifacts.CreateRunFolder(Id);

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
        var tasks = s.Urls.Select(async entry =>
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
                if (s.DelayAfterLoadMs > 0)
                {
                    await Task.Delay(s.DelayAfterLoadMs, ct).ConfigureAwait(false);
                }
                var bytes = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true }).ConfigureAwait(false);
                var name = SanitizeFileName(entry.Url) + ".png";
                var path = await ctx.Artifacts.SaveScreenshotAsync(bytes, runFolder, name).ConfigureAwait(false);
                sw.Stop();
                lock (results)
                {
                    results.Add(new RunResult(entry.Url, true, null, sw.Elapsed.TotalMilliseconds, path));
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                lock (results)
                {
                    results.Add(new RunResult(entry.Url, false, ex.Message, sw.Elapsed.TotalMilliseconds, null));
                }
            }
            finally
            {
                var current = Interlocked.Increment(ref completed);
                ctx.Progress.Report(new ProgressUpdate(current, s.Urls.Count));
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            FinishedAt = ctx.Now,
            Status = TestStatus.Completed,
            Results = results,
            Artifacts = new ReportArtifacts { ScreenshotsFolder = runFolder }
        };
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }
}

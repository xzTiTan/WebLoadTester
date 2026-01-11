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

namespace WebLoadTester.Modules.UiScenario;

public sealed class UiScenarioModule : ITestModule
{
    public string Id => "ui-scenario";
    public string DisplayName => "UI Scenario";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiScenarioSettings);

    public object CreateDefaultSettings() => new UiScenarioSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (UiScenarioSettings)settings;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.TargetUrl))
        {
            errors.Add("TargetUrl is required.");
        }
        if (s.TotalRuns <= 0)
        {
            errors.Add("TotalRuns must be positive.");
        }
        if (s.Concurrency <= 0)
        {
            errors.Add("Concurrency must be positive.");
        }
        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiScenarioSettings)settings;
        var results = new List<TestResult>();
        var runFolder = ctx.Artifacts.CreateRunFolder(Id);
        ctx.Log.Info($"Starting UI scenario at {s.TargetUrl}");

        IPlaywright playwright;
        try
        {
            playwright = await PlaywrightFactory.CreateAsync().ConfigureAwait(false);
        }
        catch (PlaywrightException ex)
        {
            ctx.Log.Error($\"Playwright error: {ex.Message}. Install browsers via 'playwright install'.\");
            throw;
        }

        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = s.Headless
        }).ConfigureAwait(false);

        var semaphore = new SemaphoreSlim(Math.Min(s.Concurrency, ctx.Limits.MaxUiConcurrency));
        var completed = 0;
        var tasks = Enumerable.Range(1, s.TotalRuns).Select(async runId =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            var sw = Stopwatch.StartNew();
            try
            {
                await using var page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.GotoAsync(s.TargetUrl).ConfigureAwait(false);
                foreach (var step in s.Steps)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        await ExecuteStepAsync(page, step, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (s.StepErrorPolicy == StepErrorPolicy.SkipStep)
                        {
                            ctx.Log.Warn($"Run {runId}: step failed: {ex.Message}");
                            continue;
                        }
                        if (s.StepErrorPolicy == StepErrorPolicy.StopAll)
                        {
                            throw;
                        }
                        break;
                    }
                }

                string? screenshotPath = null;
                if (s.ScreenshotAfterScenario)
                {
                    var bytes = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true }).ConfigureAwait(false);
                    screenshotPath = await ctx.Artifacts.SaveScreenshotAsync(bytes, runFolder, $"run_{runId}.png").ConfigureAwait(false);
                }

                sw.Stop();
                lock (results)
                {
                    results.Add(new RunResult($"Run {runId}", true, null, sw.Elapsed.TotalMilliseconds, screenshotPath));
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                lock (results)
                {
                    results.Add(new RunResult($"Run {runId}", false, ex.Message, sw.Elapsed.TotalMilliseconds, null));
                }
            }
            finally
            {
                var current = Interlocked.Increment(ref completed);
                ctx.Progress.Report(new ProgressUpdate(current, s.TotalRuns));
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

    private static async Task ExecuteStepAsync(IPage page, UiStep step, CancellationToken ct)
    {
        switch (step.Action)
        {
            case UiStepAction.WaitForVisible:
                await page.Locator(step.Selector).WaitForAsync(new LocatorWaitForOptions { Timeout = step.TimeoutMs }).ConfigureAwait(false);
                break;
            case UiStepAction.Click:
                await page.ClickAsync(step.Selector, new PageClickOptions { Timeout = step.TimeoutMs }).ConfigureAwait(false);
                break;
            case UiStepAction.FillText:
                await page.FillAsync(step.Selector, step.Text ?? string.Empty, new PageFillOptions { Timeout = step.TimeoutMs }).ConfigureAwait(false);
                break;
        }
    }
}

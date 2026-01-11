using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Playwright;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
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
        var errors = new List<string>();
        if (settings is not UiScenarioSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(s.TargetUrl))
        {
            errors.Add("TargetUrl is required");
        }

        if (s.TotalRuns <= 0)
        {
            errors.Add("TotalRuns must be > 0");
        }

        if (s.Concurrency <= 0)
        {
            errors.Add("Concurrency must be > 0");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiScenarioSettings)settings;
        var start = ctx.Now;
        var concurrency = Math.Min(s.Concurrency, ctx.Limits.MaxUiConcurrency);
        var runFolder = ctx.Artifacts.CreateRunFolder(start);
        var results = new ConcurrentBag<ResultItem>();
        var channel = Channel.CreateUnbounded<int>();
        for (var i = 1; i <= s.TotalRuns; i++)
        {
            await channel.Writer.WriteAsync(i, ct);
        }
        channel.Writer.Complete();

        IPlaywright? playwright = null;
        IBrowser? browser = null;
        try
        {
            playwright = await PlaywrightFactory.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = s.Headless });
        }
        catch (PlaywrightException ex)
        {
            ctx.Log.Log($"Playwright error: {ex.Message}. Install browsers in ./browsers.");
            throw;
        }

        var workers = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
        {
            await foreach (var runId in channel.Reader.ReadAllAsync(ct))
            {
                var sw = Stopwatch.StartNew();
                string? screenshotPath = null;
                try
                {
                    var context = await browser.NewContextAsync();
                    var page = await context.NewPageAsync();
                    await page.GotoAsync(s.TargetUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                    foreach (var step in s.Steps)
                    {
                        try
                        {
                            await ExecuteStepAsync(page, step, ct);
                        }
                        catch (Exception ex)
                        {
                            ctx.Log.Log($"Step failed: {ex.Message}");
                            if (s.StepErrorPolicy == StepErrorPolicy.StopAll)
                            {
                                throw;
                            }
                            if (s.StepErrorPolicy == StepErrorPolicy.StopRun)
                            {
                                break;
                            }
                        }
                    }

                    if (s.ScreenshotMode == UiScreenshotMode.AfterScenario)
                    {
                        var bytes = await page.ScreenshotAsync();
                        screenshotPath = await ctx.Artifacts.SaveScreenshotAsync(runFolder, bytes, $"run_{runId}.png", ct);
                    }

                    sw.Stop();
                    results.Add(new RunResult
                    {
                        Kind = "Run",
                        Name = $"Run {runId}",
                        RunId = runId,
                        Success = true,
                        DurationMs = sw.Elapsed.TotalMilliseconds,
                        ScreenshotPath = screenshotPath,
                        Url = s.TargetUrl
                    });
                    ctx.Progress.Report(new ProgressUpdate(runId, s.TotalRuns, $"Run {runId}"));
                    await context.CloseAsync();
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results.Add(new RunResult
                    {
                        Kind = "Run",
                        Name = $"Run {runId}",
                        RunId = runId,
                        Success = false,
                        DurationMs = sw.Elapsed.TotalMilliseconds,
                        ErrorMessage = ex.Message,
                        ErrorType = ex.GetType().Name,
                        ScreenshotPath = screenshotPath,
                        Url = s.TargetUrl
                    });
                }
            }
        }, ct)).ToArray();

        await Task.WhenAll(workers);
        await browser.CloseAsync();
        playwright.Dispose();

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = start,
            FinishedAt = ctx.Now,
            Status = TestStatus.Completed,
            AppVersion = AppVersionProvider.GetVersion(),
            OsDescription = Environment.OSVersion.ToString(),
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings),
            Results = results.ToList(),
            Artifacts = new ReportArtifacts(null, null, runFolder)
        };
    }

    private static Task ExecuteStepAsync(IPage page, UiScenarioStep step, CancellationToken ct)
    {
        return step.Action switch
        {
            UiStepAction.WaitForVisible => page.WaitForSelectorAsync(step.Selector, new PageWaitForSelectorOptions
            {
                Timeout = step.TimeoutMs,
                State = WaitForSelectorState.Visible
            }),
            UiStepAction.Click => page.ClickAsync(step.Selector),
            UiStepAction.FillText => page.FillAsync(step.Selector, step.Text ?? string.Empty),
            _ => Task.CompletedTask
        };
    }
}

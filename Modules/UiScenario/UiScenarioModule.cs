using System.Diagnostics;
using Microsoft.Playwright;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.UiScenario;

public sealed class UiScenarioModule : ITestModule
{
    private readonly PlaywrightFactory _factory = new();

    public string Id => "ui.scenario";
    public string DisplayName => "UI Scenario";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiScenarioSettings);

    public object CreateDefaultSettings() => new UiScenarioSettings
    {
        Steps = new List<UiScenarioStep>
        {
            new() { Action = UiStepAction.WaitForVisible, Selector = "body" }
        }
    };

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (UiScenarioSettings)settings;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.TargetUrl))
        {
            errors.Add("Target URL is required.");
        }

        if (s.Concurrency is < 1 or > 50)
        {
            errors.Add("Concurrency must be between 1 and 50.");
        }

        if (s.TotalRuns < 1)
        {
            errors.Add("Total runs must be >= 1.");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext context, CancellationToken ct)
    {
        var s = (UiScenarioSettings)settings;
        var report = CreateReportTemplate(context, s);
        var runFolder = context.Artifacts.CreateRunFolder(context.Now);
        report.Artifacts.ScreenshotsFolder = runFolder;

        using var semaphore = new SemaphoreSlim(s.Concurrency);
        var results = new List<RunResult>();
        using var playwright = await _factory.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = s.Headless
        });

        var tasks = Enumerable.Range(0, s.TotalRuns).Select(async i =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var sw = Stopwatch.StartNew();
                var result = await ExecuteRunAsync(browser, s, runFolder, i + 1, ct);
                result = result with { DurationMs = sw.ElapsedMilliseconds };
                lock (results)
                {
                    results.Add(result);
                    context.Progress.Report(results.Count, s.TotalRuns);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        report.Results.AddRange(results);
        report = report with { Metrics = Core.Services.Metrics.MetricsCalculator.Calculate(report.Results) };
        report = report with { Meta = report.Meta with { Status = TestStatus.Completed, FinishedAt = context.Now } };
        return report;
    }

    private async Task<RunResult> ExecuteRunAsync(IBrowser browser, UiScenarioSettings settings, string runFolder, int runId, CancellationToken ct)
    {
        var page = await browser.NewPageAsync();
        try
        {
            await page.GotoAsync(settings.TargetUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            foreach (var step in settings.Steps)
            {
                await ExecuteStepAsync(page, step, settings.ErrorPolicy, ct);
            }

            string? screenshotPath = null;
            if (settings.ScreenshotAfterScenario)
            {
                var bytes = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true });
                screenshotPath = Path.Combine(runFolder, $"run_{runId}.png");
                await File.WriteAllBytesAsync(screenshotPath, bytes, ct);
            }

            return new RunResult
            {
                Kind = "ui-run",
                Name = $"Run {runId}",
                Success = true,
                Url = settings.TargetUrl,
                ArtifactPath = screenshotPath
            };
        }
        catch (Exception ex)
        {
            return new RunResult
            {
                Kind = "ui-run",
                Name = $"Run {runId}",
                Success = false,
                Url = settings.TargetUrl,
                ErrorMessage = ex.Message,
                ErrorType = ex.GetType().Name
            };
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private static async Task ExecuteStepAsync(IPage page, UiScenarioStep step, StepErrorPolicy policy, CancellationToken ct)
    {
        try
        {
            await page.WaitForSelectorAsync(step.Selector, new PageWaitForSelectorOptions
            {
                Timeout = step.TimeoutMs,
                State = WaitForSelectorState.Visible
            });

            switch (step.Action)
            {
                case UiStepAction.Click:
                    await page.ClickAsync(step.Selector, new PageClickOptions { Timeout = step.TimeoutMs });
                    break;
                case UiStepAction.FillText:
                    await page.FillAsync(step.Selector, step.Text ?? string.Empty);
                    break;
            }
        }
        catch when (policy == StepErrorPolicy.SkipStep)
        {
        }
    }

    private static TestReport CreateReportTemplate(IRunContext context, UiScenarioSettings settings)
    {
        return new TestReport
        {
            Meta = new ReportMeta
            {
                Status = TestStatus.Completed,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                AppVersion = typeof(UiScenarioModule).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings)
        };
    }
}

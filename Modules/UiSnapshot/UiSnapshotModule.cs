using System.Diagnostics;
using Microsoft.Playwright;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.UiSnapshot;

public sealed class UiSnapshotModule : ITestModule
{
    private readonly PlaywrightFactory _factory = new();

    public string Id => "ui.snapshot";
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

        if (s.Concurrency is < 1 or > 20)
        {
            errors.Add("Concurrency must be between 1 and 20.");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext context, CancellationToken ct)
    {
        var s = (UiSnapshotSettings)settings;
        var report = CreateReportTemplate(context, s);
        var runFolder = context.Artifacts.CreateRunFolder(context.Now);
        report.Artifacts.ScreenshotsFolder = runFolder;

        using var playwright = await _factory.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = s.Headless
        });

        using var semaphore = new SemaphoreSlim(s.Concurrency);
        var results = new List<RunResult>();

        var tasks = s.Urls.Select(async (url, index) =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var sw = Stopwatch.StartNew();
                var page = await browser.NewPageAsync();
                try
                {
                    var wait = s.WaitMode.Equals("load", StringComparison.OrdinalIgnoreCase)
                        ? WaitUntilState.Load
                        : WaitUntilState.DOMContentLoaded;
                    await page.GotoAsync(url, new PageGotoOptions { WaitUntil = wait });
                    if (s.DelayAfterLoadMs > 0)
                    {
                        await Task.Delay(s.DelayAfterLoadMs, ct);
                    }

                    var bytes = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true });
                    var path = Path.Combine(runFolder, $"snapshot_{index + 1}.png");
                    await File.WriteAllBytesAsync(path, bytes, ct);

                    lock (results)
                    {
                        results.Add(new RunResult
                        {
                            Kind = "snapshot",
                            Name = url,
                            Success = true,
                            DurationMs = sw.ElapsedMilliseconds,
                            Url = url,
                            ArtifactPath = path
                        });
                        context.Progress.Report(results.Count, s.Urls.Count);
                    }
                }
                catch (Exception ex)
                {
                    lock (results)
                    {
                        results.Add(new RunResult
                        {
                            Kind = "snapshot",
                            Name = url,
                            Success = false,
                            DurationMs = sw.ElapsedMilliseconds,
                            Url = url,
                            ErrorMessage = ex.Message,
                            ErrorType = ex.GetType().Name
                        });
                    }
                }
                finally
                {
                    await page.CloseAsync();
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

    private static TestReport CreateReportTemplate(IRunContext context, UiSnapshotSettings settings)
    {
        return new TestReport
        {
            Meta = new ReportMeta
            {
                Status = TestStatus.Completed,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                AppVersion = typeof(UiSnapshotModule).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings)
        };
    }
}

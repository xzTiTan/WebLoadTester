using System.Diagnostics;
using Microsoft.Playwright;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.UiTiming;

public sealed class UiTimingModule : ITestModule
{
    private readonly PlaywrightFactory _factory = new();

    public string Id => "ui.timing";
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

        if (s.Concurrency is < 1 or > 20)
        {
            errors.Add("Concurrency must be between 1 and 20.");
        }

        if (s.RepeatsPerUrl < 1)
        {
            errors.Add("Repeats per URL must be >= 1.");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext context, CancellationToken ct)
    {
        var s = (UiTimingSettings)settings;
        var report = CreateReportTemplate(context, s);
        using var playwright = await _factory.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = s.Headless
        });

        var results = new List<RunResult>();
        using var semaphore = new SemaphoreSlim(s.Concurrency);
        var jobs = new List<(string url, int iteration)>();
        foreach (var url in s.Urls)
        {
            for (var i = 0; i < s.RepeatsPerUrl; i++)
            {
                jobs.Add((url, i + 1));
            }
        }

        var tasks = jobs.Select(async job =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var sw = Stopwatch.StartNew();
                var page = await browser.NewPageAsync();
                try
                {
                    var wait = s.WaitUntil.Equals("load", StringComparison.OrdinalIgnoreCase)
                        ? WaitUntilState.Load
                        : WaitUntilState.DOMContentLoaded;
                    await page.GotoAsync(job.url, new PageGotoOptions { WaitUntil = wait });
                    lock (results)
                    {
                        results.Add(new RunResult
                        {
                            Kind = "timing",
                            Name = $"{job.url} #{job.iteration}",
                            Success = true,
                            DurationMs = sw.ElapsedMilliseconds,
                            Url = job.url
                        });
                        context.Progress.Report(results.Count, jobs.Count);
                    }
                }
                catch (Exception ex)
                {
                    lock (results)
                    {
                        results.Add(new RunResult
                        {
                            Kind = "timing",
                            Name = $"{job.url} #{job.iteration}",
                            Success = false,
                            DurationMs = sw.ElapsedMilliseconds,
                            Url = job.url,
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

    private static TestReport CreateReportTemplate(IRunContext context, UiTimingSettings settings)
    {
        return new TestReport
        {
            Meta = new ReportMeta
            {
                Status = TestStatus.Completed,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                AppVersion = typeof(UiTimingModule).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings)
        };
    }
}

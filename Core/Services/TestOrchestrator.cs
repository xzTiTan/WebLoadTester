using System.Text.Json;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services.Metrics;
using WebLoadTester.Core.Services.ReportWriters;

namespace WebLoadTester.Core.Services;

public sealed class TestOrchestrator
{
    private readonly JsonReportWriter _jsonWriter = new();
    private readonly HtmlReportWriter _htmlWriter = new();

    public async Task<TestReport> ExecuteAsync(ITestModule module, object settings, IRunContext context, CancellationToken ct)
    {
        var started = context.Now;
        try
        {
            var report = await module.RunAsync(settings, context, ct);
            report = report with
            {
                Meta = report.Meta with
                {
                    ModuleId = module.Id,
                    ModuleName = module.DisplayName,
                    Family = module.Family,
                    StartedAt = started,
                    FinishedAt = context.Now,
                    Status = report.Meta.Status
                },
                Metrics = MetricsCalculator.Calculate(report.Results)
            };

            return FinalizeReport(report, context);
        }
        catch (OperationCanceledException)
        {
            var report = CreateBaseReport(module, settings, started, context.Now, TestStatus.Stopped);
            return FinalizeReport(report, context);
        }
        catch (Exception ex)
        {
            var report = CreateBaseReport(module, settings, started, context.Now, TestStatus.Error);
            report.Results.Add(new RunResult
            {
                Kind = "error",
                Name = "Unhandled exception",
                Success = false,
                DurationMs = 0,
                ErrorMessage = ex.Message,
                ErrorType = ex.GetType().Name
            });
            report = report with { Metrics = MetricsCalculator.Calculate(report.Results) };
            return FinalizeReport(report, context);
        }
    }

    public IReadOnlyList<string> Validate(ITestModule module, object settings)
        => module.Validate(settings);

    private TestReport CreateBaseReport(ITestModule module, object settings, DateTimeOffset started, DateTimeOffset finished, TestStatus status)
    {
        var snapshot = JsonSerializer.SerializeToElement(settings);
        return new TestReport
        {
            Meta = new ReportMeta
            {
                ModuleId = module.Id,
                ModuleName = module.DisplayName,
                Family = module.Family,
                StartedAt = started,
                FinishedAt = finished,
                Status = status,
                AppVersion = typeof(TestOrchestrator).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = snapshot
        };
    }

    private TestReport FinalizeReport(TestReport report, IRunContext context)
    {
        var runFolder = context.Artifacts.CreateRunFolder(report.Meta.StartedAt);
        var jsonPath = Path.Combine(context.Artifacts.ReportsFolder, $"report_{report.Meta.StartedAt:yyyyMMdd_HHmmss}.json");
        var htmlPath = Path.Combine(context.Artifacts.ReportsFolder, $"report_{report.Meta.StartedAt:yyyyMMdd_HHmmss}.html");
        report.Artifacts.JsonPath = _jsonWriter.Write(report, jsonPath);
        report.Artifacts.HtmlPath = _htmlWriter.Write(report, htmlPath);
        report.Artifacts.ScreenshotsFolder = runFolder;
        return report;
    }
}

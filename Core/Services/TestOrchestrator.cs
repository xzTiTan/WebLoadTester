using System.Text.Json;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services.Metrics;
using WebLoadTester.Core.Services.ReportWriters;

namespace WebLoadTester.Core.Services;

public sealed class TestOrchestrator
{
    private readonly JsonReportWriter _jsonWriter;
    private readonly HtmlReportWriter _htmlWriter;
    private readonly MetricsCalculator _metricsCalculator;

    public TestOrchestrator(JsonReportWriter jsonWriter, HtmlReportWriter htmlWriter, MetricsCalculator metricsCalculator)
    {
        _jsonWriter = jsonWriter;
        _htmlWriter = htmlWriter;
        _metricsCalculator = metricsCalculator;
    }

    public async Task<TestReport> RunAsync(ITestModule module, object settings, IRunContext context, CancellationToken ct)
    {
        var errors = module.Validate(settings);
        if (errors.Count > 0)
        {
            context.Log.Log($"Validation failed: {string.Join(", ", errors)}");
            return new TestReport
            {
                ModuleId = module.Id,
                ModuleName = module.DisplayName,
                Family = module.Family,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                Status = TestStatus.Error,
                AppVersion = AppVersionProvider.GetVersion(),
                OsDescription = Environment.OSVersion.ToString(),
                SettingsSnapshot = JsonSerializer.SerializeToElement(settings),
                ErrorMessage = string.Join("; ", errors)
            };
        }

        context.Log.Log($"Starting module {module.DisplayName}");
        TestReport report;
        var started = context.Now;
        try
        {
            report = await module.RunAsync(settings, context, ct);
        }
        catch (OperationCanceledException)
        {
            report = new TestReport
            {
                ModuleId = module.Id,
                ModuleName = module.DisplayName,
                Family = module.Family,
                StartedAt = started,
                FinishedAt = context.Now,
                Status = TestStatus.Stopped,
                AppVersion = AppVersionProvider.GetVersion(),
                OsDescription = Environment.OSVersion.ToString(),
                SettingsSnapshot = JsonSerializer.SerializeToElement(settings)
            };
        }
        catch (Exception ex)
        {
            context.Log.Log($"Module error: {ex.Message}");
            report = new TestReport
            {
                ModuleId = module.Id,
                ModuleName = module.DisplayName,
                Family = module.Family,
                StartedAt = started,
                FinishedAt = context.Now,
                Status = TestStatus.Error,
                AppVersion = AppVersionProvider.GetVersion(),
                OsDescription = Environment.OSVersion.ToString(),
                SettingsSnapshot = JsonSerializer.SerializeToElement(settings),
                ErrorMessage = ex.Message
            };
        }

        var metrics = _metricsCalculator.Calculate(report.Results);
        report = report with { Metrics = metrics };

        var jsonPath = await context.Artifacts.SaveJsonAsync(report, ct);
        var html = _htmlWriter.BuildHtml(report);
        var htmlPath = await context.Artifacts.SaveHtmlAsync(report, html, ct);

        report = report with { Artifacts = report.Artifacts with { JsonPath = jsonPath, HtmlPath = htmlPath } };
        context.Log.Log($"Report saved: {jsonPath}");
        return report;
    }
}

public static class AppVersionProvider
{
    public static string GetVersion() => typeof(AppVersionProvider).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services.Metrics;
using WebLoadTester.Core.Services.ReportWriters;

namespace WebLoadTester.Core.Services;

public sealed class TestOrchestrator
{
    private readonly IArtifactStore _artifactStore;
    private readonly Limits _limits;
    private readonly ITelegramNotifier? _telegramNotifier;

    public TestOrchestrator(IArtifactStore artifactStore, Limits limits, ITelegramNotifier? telegramNotifier)
    {
        _artifactStore = artifactStore;
        _limits = limits;
        _telegramNotifier = telegramNotifier;
    }

    public async Task<TestReport> RunAsync(
        ITestModule module,
        object settings,
        LogBus logBus,
        ProgressBus progressBus,
        CancellationToken ct)
    {
        var errors = module.Validate(settings);
        if (errors.Any())
        {
            logBus.Warn("Validation errors: " + string.Join("; ", errors));
            return BuildErrorReport(module, settings, errors);
        }

        var runContext = new RunContext(logBus, progressBus, _artifactStore, _limits, _telegramNotifier);
        var report = new TestReport
        {
            ModuleId = module.Id,
            ModuleName = module.DisplayName,
            Family = module.Family,
            StartedAt = runContext.Now,
            Status = TestStatus.Completed,
            AppVersion = AppVersionProvider.GetVersion(),
            OsDescription = Environment.OSVersion.VersionString,
            SettingsSnapshot = JsonSerializer.SerializeToElement(settings)
        };

        try
        {
            var moduleReport = await module.RunAsync(settings, runContext, ct).ConfigureAwait(false);
            report.Results = moduleReport.Results;
            report.Status = moduleReport.Status;
            report.Artifacts.ScreenshotsFolder = moduleReport.Artifacts.ScreenshotsFolder;
        }
        catch (OperationCanceledException)
        {
            report.Status = TestStatus.Stopped;
            logBus.Warn("Run cancelled by user.");
        }
        catch (Exception ex)
        {
            report.Status = TestStatus.Error;
            logBus.Error($"Run failed: {ex.Message}");
            report.Results.Add(new RunResult("Unhandled exception", false, ex.Message, 0, null));
        }

        report.FinishedAt = runContext.Now;
        report.Metrics = MetricsCalculator.Calculate(report.Results);

        report.Artifacts.JsonPath = await JsonReportWriter.WriteAsync(report, _artifactStore).ConfigureAwait(false);
        report.Artifacts.HtmlPath = await HtmlReportWriter.WriteAsync(report, _artifactStore).ConfigureAwait(false);

        return report;
    }

    private static TestReport BuildErrorReport(ITestModule module, object settings, IReadOnlyList<string> errors)
    {
        return new TestReport
        {
            ModuleId = module.Id,
            ModuleName = module.DisplayName,
            Family = module.Family,
            StartedAt = DateTimeOffset.Now,
            FinishedAt = DateTimeOffset.Now,
            Status = TestStatus.Error,
            AppVersion = AppVersionProvider.GetVersion(),
            OsDescription = Environment.OSVersion.VersionString,
            SettingsSnapshot = JsonSerializer.SerializeToElement(settings),
            Results = errors.Select(err => new CheckResult("Validation", false, err, 0, null, err)).ToList(),
            Metrics = MetricsCalculator.Calculate(Array.Empty<TestResult>())
        };
    }
}

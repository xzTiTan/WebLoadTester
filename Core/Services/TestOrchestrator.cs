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

/// <summary>
/// Оркестратор: валидирует настройки, запускает модуль и сохраняет отчёты.
/// </summary>
public class TestOrchestrator
{
    /// <summary>
    /// Писатель JSON-отчётов.
    /// </summary>
    private readonly JsonReportWriter _jsonWriter;
    /// <summary>
    /// Писатель HTML-отчётов.
    /// </summary>
    private readonly HtmlReportWriter _htmlWriter;
    private readonly IRunStore _runStore;

    /// <summary>
    /// Создаёт оркестратор с писателями отчётов.
    /// </summary>
    public TestOrchestrator(JsonReportWriter jsonWriter, HtmlReportWriter htmlWriter, IRunStore runStore)
    {
        _jsonWriter = jsonWriter;
        _htmlWriter = htmlWriter;
        _runStore = runStore;
    }

    /// <summary>
    /// Запускает модуль, обрабатывает ошибки и возвращает итоговый отчёт.
    /// </summary>
    public async Task<TestReport> RunAsync(ITestModule module, object settings, RunContext context, CancellationToken ct,
        ITestModule? preflightModule = null, object? preflightSettings = null)
    {
        await CreateRunRecordAsync(module, context, ct);
        var validation = module.Validate(settings);
        if (validation.Count > 0)
        {
            var report = CreateBaseReport(module, settings, context);
            report.Status = TestStatus.Failed;
            report.Results = new List<ResultBase>
            {
                new CheckResult("Validation")
                {
                    Success = false,
                    DurationMs = 0,
                    ErrorType = "Validation",
                    ErrorMessage = string.Join("; ", validation)
                }
            };
            report.Metrics = MetricsCalculator.Calculate(report.Results);
            return await FinalizeReportAsync(report, context, ct);
        }

        context.Log.Info($"Starting module {module.DisplayName}");
        TestReport resultReport;
        var preflightResults = new List<ResultBase>();
        try
        {
            if (context.Profile.PreflightEnabled && preflightModule != null && preflightSettings != null)
            {
                context.Progress.Report(new ProgressUpdate(0, 0, "Preflight"));
                var preflightReport = await preflightModule.RunAsync(preflightSettings, context, ct);
                if (preflightReport.Results.Count > 0)
                {
                    preflightResults.AddRange(preflightReport.Results.Select(r => r with { }));
                    var failed = preflightResults.Count(r => !r.Success);
                    if (failed > 0)
                    {
                        resultReport = CreateBaseReport(module, settings, context);
                        resultReport.Results.AddRange(preflightResults);
                        resultReport.Metrics = MetricsCalculator.Calculate(resultReport.Results);
                        resultReport.Status = TestStatus.Failed;
                        return await FinalizeReportAsync(resultReport, context, ct);
                    }
                }
            }

            context.Progress.Report(new ProgressUpdate(0, 0, "Running"));
            resultReport = await module.RunAsync(settings, context, ct);
            resultReport.Status = ct.IsCancellationRequested ? TestStatus.Cancelled : resultReport.Status;
        }
        catch (OperationCanceledException)
        {
            context.Log.Warn("Run cancelled.");
            resultReport = CreateBaseReport(module, settings, context);
            resultReport.Status = TestStatus.Cancelled;
        }
        catch (Exception ex)
        {
            context.Log.Error($"Run failed: {ex.Message}");
            resultReport = CreateBaseReport(module, settings, context);
            resultReport.Status = TestStatus.Failed;
            resultReport.Results.Add(new CheckResult("Exception")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = ex.GetType().Name,
                ErrorMessage = ex.ToString()
            });
        }

        if (preflightResults.Count > 0)
        {
            resultReport.Results.InsertRange(0, preflightResults);
        }

        if (resultReport.Results.Count > 0)
        {
            resultReport.Metrics = MetricsCalculator.Calculate(resultReport.Results);
        }

        return await FinalizeReportAsync(resultReport, context, ct);
    }

    /// <summary>
    /// Финализирует отчёт: сохраняет артефакты и обновляет прогресс.
    /// </summary>
    private async Task<TestReport> FinalizeReportAsync(TestReport report, RunContext context, CancellationToken ct)
    {
        context.Progress.Report(new ProgressUpdate(0, 0, "Saving"));
        report.Status = ResolveStatus(report);
        if (report.FinishedAt == default)
        {
            report.FinishedAt = DateTimeOffset.Now;
        }
        var runFolder = string.IsNullOrWhiteSpace(context.RunFolder)
            ? context.Artifacts.CreateRunFolder(report.RunId)
            : context.RunFolder;
        context.SetRunFolder(runFolder);
        report.Artifacts.ScreenshotsFolder = System.IO.Path.Combine(runFolder, "screenshots");
        report.Artifacts.LogPath = context.Artifacts.GetLogPath(report.RunId);
        report.Artifacts.JsonPath = await _jsonWriter.WriteAsync(report, runFolder);
        if (context.Profile.HtmlReportEnabled)
        {
            report.Artifacts.HtmlPath = await _htmlWriter.WriteAsync(report, runFolder);
        }
        context.Log.Info($"Report saved: {report.Artifacts.JsonPath}");
        await SaveRunArtifactsAsync(report, context, ct);
        context.Progress.Report(new ProgressUpdate(0, 0, "Done"));
        return report;
    }

    /// <summary>
    /// Создаёт базовый отчёт с метаданными и снимком настроек.
    /// </summary>
    private static TestReport CreateBaseReport(ITestModule module, object settings, RunContext context)
    {
        return new TestReport
        {
            RunId = context.RunId,
            TestCaseId = context.TestCaseId,
            TestCaseVersion = context.TestCaseVersion,
            TestName = context.TestName,
            ModuleId = module.Id,
            ModuleName = module.DisplayName,
            Family = module.Family,
            StartedAt = context.Now,
            FinishedAt = context.Now,
            Status = TestStatus.Success,
            AppVersion = typeof(TestOrchestrator).Assembly.GetName().Version?.ToString() ?? "",
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            SettingsSnapshot = JsonSerializer.Serialize(settings),
            ProfileSnapshot = context.Profile
        };
    }

    private async Task CreateRunRecordAsync(ITestModule module, RunContext context, CancellationToken ct)
    {
        var testRun = new TestRun
        {
            RunId = context.RunId,
            TestCaseId = context.TestCaseId,
            TestCaseVersion = context.TestCaseVersion,
            TestName = context.TestName,
            ModuleType = module.Id,
            ModuleName = module.DisplayName,
            ProfileSnapshotJson = JsonSerializer.Serialize(context.Profile),
            StartedAt = context.Now,
            Status = "Running"
        };

        await _runStore.CreateRunAsync(testRun, ct);
    }

    private async Task SaveRunArtifactsAsync(TestReport report, RunContext context, CancellationToken ct)
    {
        var summary = new
        {
            totalDurationMs = report.Metrics.TotalDurationMs,
            totalItems = report.Metrics.TotalItems,
            failedItems = report.Metrics.FailedItems,
            averageMs = report.Metrics.AverageMs,
            p95Ms = report.Metrics.P95Ms,
            p99Ms = report.Metrics.P99Ms
        };

        var testRun = new TestRun
        {
            RunId = report.RunId,
            TestCaseId = report.TestCaseId,
            TestCaseVersion = report.TestCaseVersion,
            TestName = report.TestName,
            ModuleType = report.ModuleId,
            ModuleName = report.ModuleName,
            ProfileSnapshotJson = JsonSerializer.Serialize(report.ProfileSnapshot),
            StartedAt = report.StartedAt,
            FinishedAt = report.FinishedAt,
            Status = report.Status.ToString(),
            SummaryJson = JsonSerializer.Serialize(summary)
        };

        await _runStore.UpdateRunAsync(testRun, ct);
        await _runStore.AddRunItemsAsync(BuildRunItems(report), ct);
        await _runStore.AddArtifactsAsync(BuildArtifacts(report, context), ct);
    }

    private static IReadOnlyList<RunItem> BuildRunItems(TestReport report)
    {
        var items = new List<RunItem>();
        foreach (var result in report.Results)
        {
            items.Add(new RunItem
            {
                Id = Guid.NewGuid(),
                RunId = report.RunId,
                ItemType = result.Kind,
                ItemKey = result switch
                {
                    RunResult run => run.Name,
                    CheckResult check => check.Name,
                    ProbeResult probe => probe.Name,
                    TimingResult timing => timing.Url ?? timing.Name,
                    _ => result.Kind
                },
                Status = result.Success ? TestStatus.Success.ToString() : TestStatus.Failed.ToString(),
                DurationMs = result.DurationMs,
                ErrorMessage = result.ErrorMessage,
                ExtraJson = BuildExtraJson(result)
            });
        }

        return items;
    }

    private static string? BuildExtraJson(ResultBase result)
    {
        var extra = result switch
        {
            RunResult run when !string.IsNullOrWhiteSpace(run.ScreenshotPath) => new { screenshot = run.ScreenshotPath },
            CheckResult check when check.StatusCode.HasValue => new { statusCode = check.StatusCode.Value },
            ProbeResult probe when !string.IsNullOrWhiteSpace(probe.Details) => new { details = probe.Details },
            TimingResult timing => new { iteration = timing.Iteration, url = timing.Url },
            _ => null
        };

        return extra == null ? null : JsonSerializer.Serialize(extra);
    }

    private static IReadOnlyList<ArtifactRecord> BuildArtifacts(TestReport report, RunContext context)
    {
        var artifacts = new List<ArtifactRecord>
        {
            new()
            {
                Id = Guid.NewGuid(),
                RunId = report.RunId,
                ArtifactType = "JsonReport",
                RelativePath = "report.json",
                CreatedAt = DateTimeOffset.Now
            },
            new()
            {
                Id = Guid.NewGuid(),
                RunId = report.RunId,
                ArtifactType = "Log",
                RelativePath = "logs/run.log",
                CreatedAt = DateTimeOffset.Now
            }
        };

        if (!string.IsNullOrWhiteSpace(report.Artifacts.HtmlPath))
        {
            artifacts.Add(new ArtifactRecord
            {
                Id = Guid.NewGuid(),
                RunId = report.RunId,
                ArtifactType = "HtmlReport",
                RelativePath = "report.html",
                CreatedAt = DateTimeOffset.Now
            });
        }

        if (System.IO.Directory.Exists(report.Artifacts.ScreenshotsFolder))
        {
            foreach (var file in System.IO.Directory.GetFiles(report.Artifacts.ScreenshotsFolder))
            {
                artifacts.Add(new ArtifactRecord
                {
                    Id = Guid.NewGuid(),
                    RunId = report.RunId,
                    ArtifactType = "Screenshot",
                    RelativePath = System.IO.Path.Combine("screenshots", System.IO.Path.GetFileName(file)),
                    CreatedAt = DateTimeOffset.Now
                });
            }
        }

        return artifacts;
    }

    private static TestStatus ResolveStatus(TestReport report)
    {
        if (report.Status == TestStatus.Cancelled)
        {
            return TestStatus.Cancelled;
        }

        if (report.Results.Count == 0)
        {
            return report.Status;
        }

        var failed = report.Results.Count(r => !r.Success);
        if (failed == 0)
        {
            return TestStatus.Success;
        }

        return failed == report.Results.Count ? TestStatus.Failed : TestStatus.Partial;
    }
}

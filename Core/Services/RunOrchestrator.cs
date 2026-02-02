using System;
using System.Collections.Concurrent;
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
/// Оркестратор: валидирует настройки, выполняет модуль и сохраняет отчёты.
/// </summary>
public class RunOrchestrator
{
    private readonly JsonReportWriter _jsonWriter;
    private readonly HtmlReportWriter _htmlWriter;
    private readonly IRunStore _runStore;

    public RunOrchestrator(JsonReportWriter jsonWriter, HtmlReportWriter htmlWriter, IRunStore runStore)
    {
        _jsonWriter = jsonWriter;
        _htmlWriter = htmlWriter;
        _runStore = runStore;
    }

    public event EventHandler<RunStageChangedEventArgs>? StageChanged;

    /// <summary>
    /// Валидирует настройки модуля и параметры профиля запуска.
    /// </summary>
    public IReadOnlyList<string> Validate(ITestModule module, object settings, RunProfile profile)
    {
        var errors = new List<string>();
        errors.AddRange(module.Validate(settings));

        if (profile.Parallelism <= 0)
        {
            errors.Add("Parallelism must be positive");
        }

        if (profile.Mode == RunMode.Iterations && profile.Iterations <= 0)
        {
            errors.Add("Iterations must be positive");
        }

        if (profile.Mode == RunMode.Duration && profile.DurationSeconds <= 0)
        {
            errors.Add("DurationSeconds must be positive");
        }

        if (profile.TimeoutSeconds <= 0)
        {
            errors.Add("TimeoutSeconds must be positive");
        }

        return errors;
    }

    /// <summary>
    /// Запускает модуль, выполняет preflight и формирует итоговый отчёт.
    /// </summary>
    public async Task<TestReport> StartAsync(
        ITestModule module,
        object settings,
        RunContext context,
        CancellationToken ct,
        ITestModule? preflightModule = null,
        object? preflightSettings = null)
    {
        var validation = Validate(module, settings, context.Profile);
        if (validation.Count > 0)
        {
            var invalidReport = CreateBaseReport(module, settings, context, context.Now);
            invalidReport.Status = TestStatus.Failed;
            invalidReport.Results.Add(new CheckResult("Validation")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = "Validation",
                ErrorMessage = string.Join("; ", validation)
            });
            invalidReport.Metrics = MetricsCalculator.Calculate(invalidReport.Results);
            return await FinalizeReportAsync(invalidReport, context, ct);
        }

        await CreateRunRecordAsync(module, context, ct);
        EnsureRunFolder(context);

        var startTime = context.Now;
        var allResults = new List<ResultBase>();
        var moduleArtifacts = new List<ModuleArtifact>();

        try
        {
            if (context.Profile.PreflightEnabled && preflightModule != null && preflightSettings != null)
            {
                SetStage(RunStage.Preflight, "Preflight");
                var preflightResult = await preflightModule.ExecuteAsync(preflightSettings, context, ct);
                allResults.AddRange(preflightResult.Results);
                moduleArtifacts.AddRange(preflightResult.Artifacts);

                if (preflightResult.Results.Any(r => !r.Success))
                {
                    var failedReport = CreateBaseReport(module, settings, context, startTime);
                    failedReport.Status = TestStatus.Failed;
                    failedReport.Results = allResults;
                    failedReport.ModuleArtifacts = moduleArtifacts;
                    failedReport.Metrics = MetricsCalculator.Calculate(failedReport.Results);
                    failedReport.FinishedAt = context.Now;
                    return await FinalizeReportAsync(failedReport, context, ct);
                }
            }

            SetStage(RunStage.Running, "Running");
            var executionResults = await ExecuteWithProfileAsync(module, settings, context, ct);
            allResults.AddRange(executionResults.Results);
            moduleArtifacts.AddRange(executionResults.Artifacts);
        }
        catch (OperationCanceledException)
        {
            context.Log.Warn("Run cancelled.");
            var cancelledReport = CreateBaseReport(module, settings, context, startTime);
            cancelledReport.Status = TestStatus.Cancelled;
            cancelledReport.Results = allResults;
            cancelledReport.ModuleArtifacts = moduleArtifacts;
            cancelledReport.FinishedAt = context.Now;
            cancelledReport.Metrics = MetricsCalculator.Calculate(cancelledReport.Results);
            return await FinalizeReportAsync(cancelledReport, context, ct);
        }
        catch (RunAbortException ex)
        {
            context.Log.Error($"Run aborted: {ex.Message}");
            var failedReport = CreateBaseReport(module, settings, context, startTime);
            failedReport.Status = TestStatus.Failed;
            failedReport.Results = allResults;
            failedReport.Results.Add(new CheckResult("Abort")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = nameof(RunAbortException),
                ErrorMessage = ex.Message
            });
            failedReport.ModuleArtifacts = moduleArtifacts;
            failedReport.FinishedAt = context.Now;
            failedReport.Metrics = MetricsCalculator.Calculate(failedReport.Results);
            return await FinalizeReportAsync(failedReport, context, ct);
        }
        catch (Exception ex)
        {
            context.Log.Error($"Run failed: {ex.Message}");
            var failedReport = CreateBaseReport(module, settings, context, startTime);
            failedReport.Status = TestStatus.Failed;
            failedReport.Results = allResults;
            failedReport.Results.Add(new CheckResult("Exception")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = ex.GetType().Name,
                ErrorMessage = ex.ToString()
            });
            failedReport.ModuleArtifacts = moduleArtifacts;
            failedReport.FinishedAt = context.Now;
            failedReport.Metrics = MetricsCalculator.Calculate(failedReport.Results);
            return await FinalizeReportAsync(failedReport, context, ct);
        }

        var report = CreateBaseReport(module, settings, context, startTime);
        report.Results = allResults;
        report.ModuleArtifacts = moduleArtifacts;
        report.FinishedAt = context.Now;
        report.Metrics = MetricsCalculator.Calculate(report.Results);
        report.Status = ResolveStatus(report.Results, ct);

        return await FinalizeReportAsync(report, context, ct);
    }

    private void EnsureRunFolder(RunContext context)
    {
        if (string.IsNullOrWhiteSpace(context.RunFolder))
        {
            var runFolder = context.Artifacts.CreateRunFolder(context.RunId);
            context.SetRunFolder(runFolder);
        }
    }

    private async Task<ModuleResult> ExecuteWithProfileAsync(ITestModule module, object settings, RunContext context, CancellationToken ct)
    {
        var mode = context.Profile.Mode;
        var parallelism = Math.Max(1, context.Profile.Parallelism);
        var moduleResults = new ConcurrentBag<ResultBase>();
        var moduleArtifacts = new ConcurrentBag<ModuleArtifact>();

        if (mode == RunMode.Iterations)
        {
            var total = context.Profile.Iterations;
            var semaphore = new SemaphoreSlim(parallelism, parallelism);
            var completed = 0;
            var tasks = Enumerable.Range(1, total).Select(async iteration =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var result = await ExecuteSafelyAsync(module, settings, context, ct, iteration.ToString());
                    foreach (var item in result.Results)
                    {
                        moduleResults.Add(item);
                    }

                    foreach (var artifact in result.Artifacts)
                    {
                        moduleArtifacts.Add(artifact);
                    }
                }
                finally
                {
                    var done = Interlocked.Increment(ref completed);
                    context.Progress.Report(new ProgressUpdate(done, total, module.DisplayName));
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);
        }
        else
        {
            var deadline = DateTimeOffset.Now.AddSeconds(context.Profile.DurationSeconds);
            var iteration = 0;
            var workers = Enumerable.Range(0, parallelism).Select(async _ =>
            {
                while (!ct.IsCancellationRequested && DateTimeOffset.Now < deadline)
                {
                    var current = Interlocked.Increment(ref iteration);
                    var result = await ExecuteSafelyAsync(module, settings, context, ct, current.ToString());
                    foreach (var item in result.Results)
                    {
                        moduleResults.Add(item);
                    }

                    foreach (var artifact in result.Artifacts)
                    {
                        moduleArtifacts.Add(artifact);
                    }

                    context.Progress.Report(new ProgressUpdate(current, 0, module.DisplayName));
                }
            }).ToList();

            await Task.WhenAll(workers);
        }

        return new ModuleResult
        {
            Results = moduleResults.ToList(),
            Artifacts = moduleArtifacts.ToList(),
            Status = moduleResults.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success
        };
    }

    private async Task<ModuleResult> ExecuteSafelyAsync(ITestModule module, object settings, RunContext context, CancellationToken ct, string iterationLabel)
    {
        try
        {
            return await module.ExecuteAsync(settings, context, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (RunAbortException)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.Log.Error($"Iteration {iterationLabel} failed: {ex.Message}");
            return new ModuleResult
            {
                Status = TestStatus.Failed,
                Results = new List<ResultBase>
                {
                    new CheckResult($"Iteration {iterationLabel}")
                    {
                        Success = false,
                        DurationMs = 0,
                        ErrorType = ex.GetType().Name,
                        ErrorMessage = ex.Message
                    }
                }
            };
        }
    }

    private void SetStage(RunStage stage, string? message)
    {
        StageChanged?.Invoke(this, new RunStageChangedEventArgs(stage, message));
    }

    private static TestReport CreateBaseReport(ITestModule module, object settings, RunContext context, DateTimeOffset startedAt)
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
            StartedAt = startedAt,
            Status = TestStatus.Success,
            AppVersion = typeof(RunOrchestrator).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            SettingsSnapshot = JsonSerializer.Serialize(settings),
            ProfileSnapshot = context.Profile
        };
    }

    private async Task<TestReport> FinalizeReportAsync(TestReport report, RunContext context, CancellationToken ct)
    {
        SetStage(RunStage.Saving, "Saving");
        report.Status = ResolveStatus(report.Results, ct);
        if (report.FinishedAt == default)
        {
            report.FinishedAt = DateTimeOffset.Now;
        }

        report.Artifacts.LogPath = context.Artifacts.GetLogPath(report.RunId);
        report.Artifacts.JsonPath = await _jsonWriter.WriteAsync(report, report.RunId);
        if (context.Profile.HtmlReportEnabled)
        {
            report.Artifacts.HtmlPath = await _htmlWriter.WriteAsync(report, report.RunId);
        }

        context.Log.Info($"Report saved: {report.Artifacts.JsonPath}");
        await SaveRunArtifactsAsync(report, context, ct);
        SetStage(RunStage.Done, "Done");
        return report;
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
        await _runStore.AddArtifactsAsync(BuildArtifacts(report), ct);
    }

    private static IReadOnlyList<RunItem> BuildRunItems(TestReport report)
    {
        return report.Results.Select(result => new RunItem
        {
            Id = Guid.NewGuid(),
            RunId = report.RunId,
            ItemType = result.Kind,
            ItemKey = result switch
            {
                RunResult run => run.Name,
                StepResult step => step.Name,
                CheckResult check => check.Name,
                PreflightResult preflight => preflight.Name,
                ProbeResult probe => probe.Name,
                TimingResult timing => timing.Url ?? timing.Name,
                _ => result.Kind
            },
            Status = result.Success ? TestStatus.Success.ToString() : TestStatus.Failed.ToString(),
            DurationMs = result.DurationMs,
            ErrorMessage = result.ErrorMessage,
            ExtraJson = BuildExtraJson(result)
        }).ToList();
    }

    private static string? BuildExtraJson(ResultBase result)
    {
        Dictionary<string, object?>? extra = result switch
        {
            RunResult run when !string.IsNullOrWhiteSpace(run.ScreenshotPath) => new Dictionary<string, object?>
            {
                ["screenshot"] = run.ScreenshotPath
            },
            StepResult step => new Dictionary<string, object?>
            {
                ["action"] = step.Action,
                ["selector"] = step.Selector,
                ["screenshot"] = step.ScreenshotPath
            },
            CheckResult check when check.StatusCode.HasValue => new Dictionary<string, object?>
            {
                ["statusCode"] = check.StatusCode.Value
            },
            PreflightResult preflight => new Dictionary<string, object?>
            {
                ["statusCode"] = preflight.StatusCode,
                ["details"] = preflight.Details
            },
            ProbeResult probe when !string.IsNullOrWhiteSpace(probe.Details) => new Dictionary<string, object?>
            {
                ["details"] = probe.Details
            },
            TimingResult timing => new Dictionary<string, object?>
            {
                ["iteration"] = timing.Iteration,
                ["url"] = timing.Url
            },
            _ => null
        };

        return extra == null ? null : JsonSerializer.Serialize(extra);
    }

    private static IReadOnlyList<ArtifactRecord> BuildArtifacts(TestReport report)
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

        var screenshotPaths = report.Results
            .Select(result => result switch
            {
                RunResult run => run.ScreenshotPath,
                StepResult step => step.ScreenshotPath,
                _ => null
            })
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct()
            .ToList();

        foreach (var path in screenshotPaths)
        {
            artifacts.Add(new ArtifactRecord
            {
                Id = Guid.NewGuid(),
                RunId = report.RunId,
                ArtifactType = "Screenshot",
                RelativePath = path!,
                CreatedAt = DateTimeOffset.Now
            });
        }

        foreach (var artifact in report.ModuleArtifacts)
        {
            artifacts.Add(new ArtifactRecord
            {
                Id = Guid.NewGuid(),
                RunId = report.RunId,
                ArtifactType = artifact.Type,
                RelativePath = artifact.RelativePath,
                CreatedAt = DateTimeOffset.Now
            });
        }

        return artifacts;
    }

    private static TestStatus ResolveStatus(IEnumerable<ResultBase> results, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return TestStatus.Cancelled;
        }

        var list = results.ToList();
        if (list.Count == 0)
        {
            return TestStatus.Success;
        }

        var failed = list.Count(r => !r.Success);
        if (failed == 0)
        {
            return TestStatus.Success;
        }

        return failed == list.Count ? TestStatus.Failed : TestStatus.Partial;
    }
}

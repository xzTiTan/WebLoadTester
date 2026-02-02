using System.Linq;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

/// <summary>
/// Писатель JSON-отчётов через хранилище артефактов.
/// </summary>
public class JsonReportWriter
{
    private readonly IArtifactStore _artifactStore;

    /// <summary>
    /// Создаёт писатель с заданным хранилищем.
    /// </summary>
    public JsonReportWriter(IArtifactStore artifactStore)
    {
        _artifactStore = artifactStore;
    }

    /// <summary>
    /// Сохраняет отчёт в JSON и возвращает путь к файлу.
    /// </summary>
    public Task<string> WriteAsync(TestReport report, string runId)
    {
        var payload = new
        {
            run = new
            {
                runId = report.RunId,
                startedAt = report.StartedAt,
                finishedAt = report.FinishedAt,
                status = report.Status.ToString(),
                moduleType = report.ModuleId,
                moduleNameRu = report.ModuleName,
                testName = report.TestName,
                testCaseId = report.TestCaseId,
                testCaseVersion = report.TestCaseVersion
            },
            environment = new
            {
                os = report.OsDescription,
                appVersion = report.AppVersion,
                machineName = System.Environment.MachineName
            },
            profile = new
            {
                parallelism = report.ProfileSnapshot.Parallelism,
                mode = report.ProfileSnapshot.Mode.ToString().ToLowerInvariant(),
                iterations = report.ProfileSnapshot.Iterations,
                durationSeconds = report.ProfileSnapshot.DurationSeconds,
                timeouts = new { operationSeconds = report.ProfileSnapshot.TimeoutSeconds },
                headless = report.ProfileSnapshot.Headless,
                screenshotsPolicy = report.ProfileSnapshot.ScreenshotsPolicy.ToString(),
                htmlReportEnabled = report.ProfileSnapshot.HtmlReportEnabled,
                telegramEnabled = report.ProfileSnapshot.TelegramEnabled,
                preflightEnabled = report.ProfileSnapshot.PreflightEnabled
            },
            summary = new
            {
                totalDurationMs = report.Metrics.TotalDurationMs,
                totalItems = report.Metrics.TotalItems,
                failedItems = report.Metrics.FailedItems,
                averageMs = report.Metrics.AverageMs,
                p95Ms = report.Metrics.P95Ms,
                p99Ms = report.Metrics.P99Ms
            },
            details = report.Results.ConvertAll(result => new
            {
                key = result switch
                {
                    RunResult run => run.Name,
                    StepResult step => step.Name,
                    CheckResult check => check.Name,
                    PreflightResult preflight => preflight.Name,
                    ProbeResult probe => probe.Name,
                    TimingResult timing => timing.Url ?? timing.Name,
                    _ => result.Kind
                },
                status = result.Success ? "Success" : "Failed",
                durationMs = result.DurationMs,
                errorMessage = result.ErrorMessage,
                extra = BuildExtra(result)
            }),
            artifacts = BuildArtifacts(report)
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        return _artifactStore.SaveJsonReportAsync(runId, json);
    }

    private static object? BuildExtra(ResultBase result)
    {
        return result switch
        {
            RunResult run => string.IsNullOrWhiteSpace(run.ScreenshotPath) ? null : new { screenshot = run.ScreenshotPath },
            StepResult step => new { action = step.Action, selector = step.Selector, screenshot = step.ScreenshotPath },
            CheckResult check => check.StatusCode.HasValue ? new { statusCode = check.StatusCode } : null,
            PreflightResult preflight => new { statusCode = preflight.StatusCode, details = preflight.Details },
            ProbeResult probe => string.IsNullOrWhiteSpace(probe.Details) ? null : new { details = probe.Details },
            TimingResult timing => new { iteration = timing.Iteration, url = timing.Url },
            _ => null
        };
    }

    private static List<object> BuildArtifacts(TestReport report)
    {
        var artifacts = new List<object>
        {
            new { type = "JsonReport", relativePath = "report.json" }
        };

        if (!string.IsNullOrWhiteSpace(report.Artifacts.HtmlPath))
        {
            artifacts.Add(new { type = "HtmlReport", relativePath = "report.html" });
        }

        artifacts.Add(new { type = "Log", relativePath = "logs/run.log" });

        var screenshotPaths = report.Results
            .Select(result => result switch
            {
                RunResult run => run.ScreenshotPath,
                StepResult step => step.ScreenshotPath,
                _ => null
            })
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct();
        foreach (var path in screenshotPaths)
        {
            artifacts.Add(new
            {
                type = "Screenshot",
                relativePath = path
            });
        }

        foreach (var artifact in report.ModuleArtifacts)
        {
            artifacts.Add(new { type = artifact.Type, relativePath = artifact.RelativePath });
        }

        return artifacts;
    }
}

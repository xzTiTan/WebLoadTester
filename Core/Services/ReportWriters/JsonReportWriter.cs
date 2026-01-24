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
    public Task<string> WriteAsync(TestReport report, string runFolder)
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
                    CheckResult check => check.Name,
                    ProbeResult probe => probe.Name,
                    TimingResult timing => timing.Url ?? timing.Name,
                    _ => result.Kind
                },
                status = result.Success ? "Success" : "Failed",
                durationMs = result.DurationMs,
                errorMessage = result.ErrorMessage,
                extra = BuildExtra(result)
            }),
            artifacts = BuildArtifacts(report, runFolder)
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        return _artifactStore.SaveJsonAsync(json, runFolder);
    }

    private static object? BuildExtra(ResultBase result)
    {
        return result switch
        {
            RunResult run => string.IsNullOrWhiteSpace(run.ScreenshotPath) ? null : new { screenshot = run.ScreenshotPath },
            CheckResult check => check.StatusCode.HasValue ? new { statusCode = check.StatusCode } : null,
            ProbeResult probe => string.IsNullOrWhiteSpace(probe.Details) ? null : new { details = probe.Details },
            TimingResult timing => new { iteration = timing.Iteration, url = timing.Url },
            _ => null
        };
    }

    private static List<object> BuildArtifacts(TestReport report, string runFolder)
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

        if (System.IO.Directory.Exists(report.Artifacts.ScreenshotsFolder))
        {
            foreach (var file in System.IO.Directory.GetFiles(report.Artifacts.ScreenshotsFolder))
            {
                artifacts.Add(new
                {
                    type = "Screenshot",
                    relativePath = System.IO.Path.Combine("screenshots", System.IO.Path.GetFileName(file))
                });
            }
        }

        return artifacts;
    }
}

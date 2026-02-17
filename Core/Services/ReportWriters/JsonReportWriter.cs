using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

    public JsonReportWriter(IArtifactStore artifactStore)
    {
        _artifactStore = artifactStore;
    }

    public Task<string> WriteAsync(TestReport report, string runId)
    {
        var payload = new
        {
            runId = report.RunId,
            moduleId = report.ModuleId,
            finalName = string.IsNullOrWhiteSpace(report.FinalName) ? report.TestName : report.FinalName,
            startedAtUtc = report.StartedAt,
            finishedAtUtc = report.FinishedAt,
            status = report.Status.ToString(),
            profile = new
            {
                parallelism = report.ProfileSnapshot.Parallelism,
                mode = report.ProfileSnapshot.Mode.ToString().ToLowerInvariant(),
                iterations = report.ProfileSnapshot.Iterations,
                durationSeconds = report.ProfileSnapshot.DurationSeconds,
                pauseBetweenIterationsMs = report.ProfileSnapshot.PauseBetweenIterationsMs,
                timeouts = new { operationSeconds = report.ProfileSnapshot.TimeoutSeconds },
                headless = report.ProfileSnapshot.Headless,
                screenshotsPolicy = report.ProfileSnapshot.ScreenshotsPolicy.ToString(),
                htmlReportEnabled = report.ProfileSnapshot.HtmlReportEnabled,
                telegramEnabled = report.ProfileSnapshot.TelegramEnabled,
                preflightEnabled = report.ProfileSnapshot.PreflightEnabled
            },
            moduleSettings = BuildModuleSettings(report),
            summary = new
            {
                durationMs = report.Metrics.TotalDurationMs,
                totalItems = report.Metrics.TotalItems,
                failedItems = report.Metrics.FailedItems,
                workers = report.ProfileSnapshot.Parallelism,
                averageMs = report.Metrics.AverageMs,
                p95Ms = report.Metrics.P95Ms,
                p99Ms = report.Metrics.P99Ms
            },
            items = report.Results.ConvertAll(result =>
            {
                var artifactRefs = BuildArtifactRefs(result);
                return new
                {
                    kind = result.Kind,
                    workerId = result.WorkerId,
                    iteration = result.IterationIndex,
                    itemIndex = result.ItemIndex,
                    name = ResolveName(result),
                    ok = result.Success,
                    severity = result.Severity,
                    message = result.ErrorMessage,
                    errorKind = result.ErrorType,
                    durationMs = result.DurationMs,
                    artifactRefs,
                    metrics = BuildMetrics(result),
                    extra = BuildExtra(result)
                };
            }),
            artifacts = BuildArtifacts(report)
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        return _artifactStore.SaveJsonReportAsync(runId, json);
    }

    private static string ResolveName(ResultBase result)
    {
        return result switch
        {
            RunResult run => run.Name,
            StepResult step => step.Name,
            CheckResult check => check.Name,
            EndpointResult endpoint => endpoint.Name,
            AssetResult asset => asset.Name,
            PreflightResult preflight => preflight.Name,
            ProbeResult probe => probe.Name,
            TimingResult timing => timing.Url ?? timing.Name,
            _ => result.Kind
        };
    }

    private static JsonElement BuildModuleSettings(TestReport report)
    {
        if (report.ModuleSettingsSnapshot.ValueKind != JsonValueKind.Undefined)
        {
            return report.ModuleSettingsSnapshot;
        }

        if (!string.IsNullOrWhiteSpace(report.SettingsSnapshot))
        {
            try
            {
                return JsonSerializer.SerializeToElement(JsonSerializer.Deserialize<JsonElement>(report.SettingsSnapshot));
            }
            catch (JsonException)
            {
                // Ignore malformed legacy snapshot and fallback to empty object.
            }
        }

        return JsonSerializer.SerializeToElement(new { });
    }

    private static object? BuildExtra(ResultBase result)
    {
        return result switch
        {
            RunResult run => new { screenshot = run.ScreenshotPath, detailsJson = run.DetailsJson },
            StepResult step => new { action = step.Action, selector = step.Selector, screenshot = step.ScreenshotPath, detailsJson = step.DetailsJson },
            PreflightResult preflight => preflight.StatusCode.HasValue ? new { statusCode = preflight.StatusCode, details = preflight.Details } : new { details = preflight.Details },
            ProbeResult probe => string.IsNullOrWhiteSpace(probe.Details) ? null : new { details = probe.Details },
            TimingResult timing => new { iteration = timing.Iteration, url = timing.Url, detailsJson = timing.DetailsJson },
            _ => null
        };
    }

    private static List<string> BuildArtifactRefs(ResultBase result)
    {
        var refs = new List<string>();
        switch (result)
        {
            case RunResult run when !string.IsNullOrWhiteSpace(run.ScreenshotPath):
                refs.Add(run.ScreenshotPath!);
                break;
            case StepResult step when !string.IsNullOrWhiteSpace(step.ScreenshotPath):
                refs.Add(step.ScreenshotPath!);
                break;
        }

        return refs;
    }

    private static object? BuildMetrics(ResultBase result)
    {
        return result switch
        {
            RunResult run => ParseDetails(run.DetailsJson),
            StepResult step => ParseDetails(step.DetailsJson),
            TimingResult timing => ParseDetails(timing.DetailsJson),
            CheckResult check => check.Metrics ?? JsonSerializer.SerializeToElement(new { statusCode = check.StatusCode }),
            EndpointResult endpoint => new { statusCode = endpoint.StatusCode, latencyMs = endpoint.LatencyMs },
            AssetResult asset => new { statusCode = asset.StatusCode, latencyMs = asset.LatencyMs, bytes = asset.Bytes, contentType = asset.ContentType },
            PreflightResult preflight => preflight.Metrics,
            _ => null
        };
    }

    private static JsonElement? ParseDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch
        {
            return null;
        }
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

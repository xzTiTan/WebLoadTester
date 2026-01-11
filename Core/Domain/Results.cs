using System.Text.Json;

namespace WebLoadTester.Core.Domain;

public abstract record ResultItem
{
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Success { get; init; }
    public double DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorType { get; init; }
}

public sealed record RunResult : ResultItem
{
    public int RunId { get; init; }
    public string? ScreenshotPath { get; init; }
    public string? Url { get; init; }
}

public sealed record CheckResult : ResultItem
{
    public int? StatusCode { get; init; }
    public string? Details { get; init; }
}

public sealed record ProbeResult : ResultItem
{
    public string? Target { get; init; }
    public string? Severity { get; init; }
    public Dictionary<string, string>? Data { get; init; }
}

public sealed record MetricsSummary(
    double AvgMs,
    double MinMs,
    double MaxMs,
    double P50,
    double P95,
    double P99,
    Dictionary<string, int> ErrorBreakdown,
    List<ResultItem> Slowest);

public sealed record ReportArtifacts(
    string? JsonPath,
    string? HtmlPath,
    string? ScreenshotsFolder);

public sealed record TestReport
{
    public string ModuleId { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public TestFamily Family { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; init; }
    public TestStatus Status { get; init; }
    public string AppVersion { get; init; } = string.Empty;
    public string OsDescription { get; init; } = string.Empty;
    public JsonElement SettingsSnapshot { get; init; }
    public List<ResultItem> Results { get; init; } = new();
    public MetricsSummary? Metrics { get; init; }
    public ReportArtifacts Artifacts { get; init; } = new(null, null, null);
    public string? ErrorMessage { get; init; }
}

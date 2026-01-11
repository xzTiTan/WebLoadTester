using System.Text.Json;

namespace WebLoadTester.Core.Domain;

public sealed record TestReport
{
    public ReportMeta Meta { get; init; } = new();
    public JsonElement SettingsSnapshot { get; init; }
    public List<ResultItem> Results { get; init; } = [];
    public MetricsSummary Metrics { get; init; } = new();
    public ReportArtifacts Artifacts { get; init; } = new();
}

public sealed record ReportMeta
{
    public string ModuleId { get; init; } = string.Empty;
    public string ModuleName { get; init; } = string.Empty;
    public TestFamily Family { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset FinishedAt { get; init; }
    public TestStatus Status { get; init; }
    public string AppVersion { get; init; } = string.Empty;
    public string OperatingSystem { get; init; } = string.Empty;
}

public sealed record ReportArtifacts
{
    public string? JsonPath { get; set; }
    public string? HtmlPath { get; set; }
    public string? ScreenshotsFolder { get; set; }
}

public abstract record ResultItem
{
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Success { get; init; }
    public long DurationMs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorType { get; init; }
    public int? StatusCode { get; init; }
    public string? ArtifactPath { get; init; }
}

public sealed record RunResult : ResultItem
{
    public string? Url { get; init; }
}

public sealed record CheckResult : ResultItem
{
    public string? Details { get; init; }
}

public sealed record ProbeResult : ResultItem
{
    public string? Target { get; init; }
}

public sealed record MetricsSummary
{
    public double AverageMs { get; init; }
    public double MinMs { get; init; }
    public double MaxMs { get; init; }
    public double P50Ms { get; init; }
    public double P95Ms { get; init; }
    public double P99Ms { get; init; }
    public Dictionary<string, int> ErrorBreakdown { get; init; } = new();
    public List<ResultItem> TopSlow { get; init; } = [];
}

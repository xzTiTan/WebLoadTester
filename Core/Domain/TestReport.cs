using System;
using System.Collections.Generic;

namespace WebLoadTester.Core.Domain;

public class TestReport
{
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public TestFamily Family { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public TestStatus Status { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string OsDescription { get; set; } = string.Empty;
    public string SettingsSnapshot { get; set; } = string.Empty;
    public List<ResultBase> Results { get; set; } = new();
    public MetricsSummary Metrics { get; set; } = new();
    public ArtifactInfo Artifacts { get; set; } = new();
}

public class MetricsSummary
{
    public double AverageMs { get; set; }
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public Dictionary<string, int> ErrorBreakdown { get; set; } = new();
    public List<ResultBase> TopSlow { get; set; } = new();
}

public class ArtifactInfo
{
    public string JsonPath { get; set; } = string.Empty;
    public string HtmlPath { get; set; } = string.Empty;
    public string ScreenshotsFolder { get; set; } = string.Empty;
}

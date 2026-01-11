using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WebLoadTester.Core.Domain;

public sealed class TestReport
{
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public TestFamily Family { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public TestStatus Status { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string OsDescription { get; set; } = string.Empty;
    public JsonElement? SettingsSnapshot { get; set; }
    public List<TestResult> Results { get; set; } = new();
    public MetricsSummary? Metrics { get; set; }
    public ReportArtifacts Artifacts { get; set; } = new();
}

public sealed class ReportArtifacts
{
    public string? JsonPath { get; set; }
    public string? HtmlPath { get; set; }
    public string? ScreenshotsFolder { get; set; }
}

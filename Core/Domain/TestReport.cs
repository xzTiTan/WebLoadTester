using System;
using System.Collections.Generic;

namespace WebLoadTester.Core.Domain;

/// <summary>
/// Итоговый отчёт о запуске тестового модуля.
/// </summary>
public class TestReport
{
    public string RunId { get; set; } = string.Empty;
    public Guid TestCaseId { get; set; }
    public int TestCaseVersion { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string ModuleId { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public TestFamily Family { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public TestStatus Status { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public string OsDescription { get; set; } = string.Empty;
    public string SettingsSnapshot { get; set; } = string.Empty;
    public RunProfile ProfileSnapshot { get; set; } = new();
    public List<ResultBase> Results { get; set; } = new();
    public MetricsSummary Metrics { get; set; } = new();
    public ArtifactInfo Artifacts { get; set; } = new();
    public List<ModuleArtifact> ModuleArtifacts { get; set; } = new();

    public string TotalDurationText => $"{Metrics.TotalDurationMs:F0} мс";
    public string AverageText => $"{Metrics.AverageMs:F1} мс";
    public string P95Text => $"{Metrics.P95Ms:F1} мс";
}

/// <summary>
/// Сводные метрики по результатам теста.
/// </summary>
public class MetricsSummary
{
    public double AverageMs { get; set; }
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public double P50Ms { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double TotalDurationMs { get; set; }
    public int TotalItems { get; set; }
    public int FailedItems { get; set; }
    public Dictionary<string, int> ErrorBreakdown { get; set; } = new();
    public List<ResultBase> TopSlow { get; set; } = new();
}

/// <summary>
/// Пути к сгенерированным артефактам отчёта.
/// </summary>
public class ArtifactInfo
{
    public string JsonPath { get; set; } = string.Empty;
    public string HtmlPath { get; set; } = string.Empty;
    public string ScreenshotsFolder { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
}

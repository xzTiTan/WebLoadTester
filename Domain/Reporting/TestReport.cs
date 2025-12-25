using System;
using System.Collections.Generic;
using WebLoadTester.Domain;

namespace WebLoadTester.Domain.Reporting
{
    public class TestReport
    {
        public ReportMeta Meta { get; set; } = new();
        public RunSettings Settings { get; set; } = new();
        public List<string> ScenarioSelectors { get; set; } = new();
        public List<TestPhase> Phases { get; set; } = new();
        public ReportSummary Summary { get; set; } = new();
        public List<PhaseSummary> PhaseSummaries { get; set; } = new();
        public List<ErrorBreakdownEntry> ErrorBreakdown { get; set; } = new();
        public List<SlowRunInfo> TopSlowRuns { get; set; } = new();
        public List<RunResult> Runs { get; set; } = new();
    }

    public class ReportMeta
    {
        public string AppName { get; set; } = "WebLoadTester";
        public string AppVersion { get; set; } = "";
        public string OsDescription { get; set; } = "";
        public DateTime StartedAtUtc { get; set; }
        public DateTime FinishedAtUtc { get; set; }
        public string Status { get; set; } = "Completed";
    }

    public class ReportSummary
    {
        public int TotalRunsPlanned { get; set; }
        public int TotalRunsExecuted { get; set; }
        public int Ok { get; set; }
        public int Fail { get; set; }
        public long TotalDurationMs { get; set; }
        public double AvgMs { get; set; }
        public long MaxMs { get; set; }
        public long MinMs { get; set; }
        public long P50 { get; set; }
        public long P90 { get; set; }
        public long P95 { get; set; }
        public long P99 { get; set; }
    }

    public class PhaseSummary
    {
        public string PhaseName { get; set; } = string.Empty;
        public int RunsExecuted { get; set; }
        public int Ok { get; set; }
        public int Fail { get; set; }
        public double AvgMs { get; set; }
        public long MaxMs { get; set; }
        public long P95 { get; set; }
    }

    public class ErrorBreakdownEntry
    {
        public string ErrorType { get; set; } = string.Empty;
        public int Count { get; set; }
        public string? SampleMessage { get; set; }
    }

    public class SlowRunInfo
    {
        public int RunId { get; set; }
        public int WorkerId { get; set; }
        public string Phase { get; set; } = string.Empty;
        public long DurationMs { get; set; }
        public bool Success { get; set; }
        public string? ScreenshotPath { get; set; }
    }
}

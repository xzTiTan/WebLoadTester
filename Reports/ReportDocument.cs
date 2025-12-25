using System;
using System.Collections.Generic;
using WebLoadTester.Domain;

namespace WebLoadTester.Reports
{
    public class ReportDocument
    {
        public ReportMeta Meta { get; set; } = new();
        public TestType TestType { get; set; }
        public RunSettings Settings { get; set; } = new();
        public ReportSummary Summary { get; set; } = new();
        public List<RunResult> Runs { get; set; } = new();
    }

    public class ReportMeta
    {
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
        public string AppVersion { get; set; } = "1.0";
        public string Os { get; set; } = Environment.OSVersion.ToString();
    }

    public class ReportSummary
    {
        public int TotalRuns { get; set; }
        public int Ok { get; set; }
        public int Fail { get; set; }
        public double AvgDurationMs { get; set; }
        public double MinDurationMs { get; set; }
        public double MaxDurationMs { get; set; }
        public double P95DurationMs { get; set; }
    }
}

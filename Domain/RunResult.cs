using System;
using System.Collections.Generic;

namespace WebLoadTester.Domain
{
    public class RunResult
    {
        public int WorkerId { get; set; }
        public int RunId { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? FinalUrl { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ScreenshotPath { get; set; }
        public List<StepResult> Steps { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime FinishedAt { get; set; }
    }

    public class StepResult
    {
        public string Selector { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
    }
}

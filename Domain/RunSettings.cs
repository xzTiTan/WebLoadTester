using System;
using System.Collections.Generic;

namespace WebLoadTester.Domain
{
    public class RunSettings
    {
        public string TargetUrl { get; set; } = string.Empty;
        public int TotalRuns { get; set; } = 1;
        public int Concurrency { get; set; } = 1;
        public int TimeoutSeconds { get; set; } = 30;
        public bool Headless { get; set; } = true;
        public bool ScreenshotAfterRun { get; set; }
        public string? ScreenshotDirectory { get; set; }
        public StepErrorPolicy StepErrorPolicy { get; set; } = StepErrorPolicy.SkipStep;
        public StepErrorPolicy ErrorPolicy
        {
            get => StepErrorPolicy;
            set => StepErrorPolicy = value;
        }

        public TestType TestType { get; set; } = TestType.E2E;

        public int StressStep { get; set; } = 5;
        public int StressPauseSeconds { get; set; } = 10;
        public int RunsPerLevel { get; set; } = 10;
        public int EnduranceMinutes { get; set; } = 10;

        public StressSettings Stress { get; set; } = new();
        public EnduranceSettings Endurance { get; set; } = new();

        public TelegramSettings Telegram { get; set; } = new();
        public List<string> Assertions { get; set; } = new();
    }

    [Obsolete("Use StepErrorPolicy instead")] 
    public class StressSettings
    {
        public int RampStep { get; set; } = 5;
        public int RampDelaySeconds { get; set; } = 10;
        public int RunsPerLevel { get; set; } = 10;
    }

    [Obsolete("Use EnduranceMinutes instead")]
    public class EnduranceSettings
    {
        public int DurationMinutes { get; set; } = 10;
    }

    public class TelegramSettings
    {
        public bool Enabled { get; set; }
        public bool SendScreenshots { get; set; }
        public int Mode { get; set; }
        public string Token { get; set; } = string.Empty;
        public string ChatId { get; set; } = string.Empty;
    }
}

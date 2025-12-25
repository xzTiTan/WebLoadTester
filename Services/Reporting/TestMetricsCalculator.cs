using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using WebLoadTester.Domain;
using WebLoadTester.Domain.Reporting;

namespace WebLoadTester.Services.Reporting
{
    public static class TestMetricsCalculator
    {
        public static TestReport BuildReport(
            RunSettings settings,
            List<string> selectors,
            List<TestPhase> phases,
            List<RunResult> runs,
            DateTime startedUtc,
            DateTime finishedUtc,
            string status,
            int plannedRuns)
        {
            var durations = runs.Select(r => GetDurationMs(r)).OrderBy(x => x).ToList();
            var totalRunsExecuted = runs.Count;
            var ok = runs.Count(r => r.Success);
            var fail = totalRunsExecuted - ok;

            var summary = new ReportSummary
            {
                TotalRunsPlanned = plannedRuns,
                TotalRunsExecuted = totalRunsExecuted,
                Ok = ok,
                Fail = fail,
                TotalDurationMs = (long)(finishedUtc - startedUtc).TotalMilliseconds,
                AvgMs = durations.Count == 0 ? 0 : durations.Average(),
                MinMs = durations.Count == 0 ? 0 : durations.First(),
                MaxMs = durations.Count == 0 ? 0 : durations.Last(),
                P50 = Percentile(durations, 0.50),
                P90 = Percentile(durations, 0.90),
                P95 = Percentile(durations, 0.95),
                P99 = Percentile(durations, 0.99)
            };

            var phaseSummaries = runs
                .GroupBy(r => r.PhaseName)
                .Select(g =>
                {
                    var list = g.Select(GetDurationMs).OrderBy(x => x).ToList();
                    var okCount = g.Count(r => r.Success);
                    var failCount = g.Count() - okCount;
                    return new PhaseSummary
                    {
                        PhaseName = g.Key,
                        RunsExecuted = g.Count(),
                        Ok = okCount,
                        Fail = failCount,
                        AvgMs = list.Count == 0 ? 0 : list.Average(),
                        MaxMs = list.Count == 0 ? 0 : list.Last(),
                        P95 = Percentile(list, 0.95)
                    };
                })
                .OrderBy(p => p.PhaseName)
                .ToList();

            var errorBreakdown = runs
                .Where(r => !r.Success)
                .GroupBy(r => string.IsNullOrWhiteSpace(r.ErrorType) ? "Unknown" : r.ErrorType!)
                .Select(g => new ErrorBreakdownEntry
                {
                    ErrorType = g.Key,
                    Count = g.Count(),
                    SampleMessage = TrimSample(g.Select(r => r.ErrorMessage).FirstOrDefault())
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            var topSlowRuns = runs
                .OrderByDescending(r => GetDurationMs(r))
                .Take(10)
                .Select(r => new SlowRunInfo
                {
                    RunId = r.RunId,
                    WorkerId = r.WorkerId,
                    Phase = r.PhaseName,
                    DurationMs = GetDurationMs(r),
                    Success = r.Success,
                    ScreenshotPath = r.ScreenshotPath
                })
                .ToList();

            return new TestReport
            {
                Meta = new ReportMeta
                {
                    AppName = "WebLoadTester",
                    AppVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0",
                    OsDescription = RuntimeInformation.OSDescription,
                    StartedAtUtc = startedUtc,
                    FinishedAtUtc = finishedUtc,
                    Status = status
                },
                Settings = settings,
                ScenarioSelectors = selectors,
                Phases = phases,
                Summary = summary,
                PhaseSummaries = phaseSummaries,
                ErrorBreakdown = errorBreakdown,
                TopSlowRuns = topSlowRuns,
                Runs = runs
            };
        }

        private static long GetDurationMs(RunResult result)
        {
            if (result.DurationMs > 0)
            {
                return result.DurationMs;
            }

            return (long)result.Duration.TotalMilliseconds;
        }

        private static long Percentile(IReadOnlyList<long> sortedDurations, double percentile)
        {
            if (sortedDurations.Count == 0)
            {
                return 0;
            }

            var index = (int)Math.Ceiling(percentile * sortedDurations.Count) - 1;
            index = Math.Max(0, Math.Min(index, sortedDurations.Count - 1));
            return sortedDurations[index];
        }

        private static string? TrimSample(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            const int maxLen = 120;
            return message!.Length <= maxLen ? message : message.Substring(0, maxLen);
        }
    }
}

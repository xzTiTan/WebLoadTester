using System;
using System.Collections.Generic;
using System.Linq;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.Metrics;

public static class MetricsCalculator
{
    public static MetricsSummary Calculate(IEnumerable<TestResult> results)
    {
        var list = results.ToList();
        var durations = list.Select(r => r.DurationMs).Where(d => d >= 0).OrderBy(d => d).ToList();
        double min = durations.Count > 0 ? durations.First() : 0;
        double max = durations.Count > 0 ? durations.Last() : 0;
        double avg = durations.Count > 0 ? durations.Average() : 0;
        double p50 = Percentile(durations, 0.50);
        double p95 = Percentile(durations, 0.95);
        double p99 = Percentile(durations, 0.99);

        var breakdown = list
            .Where(r => !r.Success)
            .GroupBy(r => r.ErrorMessage ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        var topSlow = list.OrderByDescending(r => r.DurationMs).Take(5).ToList();

        return new MetricsSummary(min, max, avg, p50, p95, p99, breakdown, topSlow);
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var position = (sorted.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }

        var weight = position - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }
}

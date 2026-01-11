using System;
using System.Collections.Generic;
using System.Linq;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.Metrics;

public static class MetricsCalculator
{
    public static MetricsSummary Calculate(IEnumerable<ResultBase> results)
    {
        var list = results.ToList();
        var durations = list.Select(r => r.DurationMs).Where(d => d >= 0).OrderBy(d => d).ToList();
        var summary = new MetricsSummary();

        if (durations.Count > 0)
        {
            summary.MinMs = durations.First();
            summary.MaxMs = durations.Last();
            summary.AverageMs = durations.Average();
            summary.P50Ms = Percentile(durations, 0.50);
            summary.P95Ms = Percentile(durations, 0.95);
            summary.P99Ms = Percentile(durations, 0.99);
        }

        summary.ErrorBreakdown = list
            .Where(r => !r.Success && !string.IsNullOrWhiteSpace(r.ErrorType))
            .GroupBy(r => r.ErrorType!)
            .ToDictionary(g => g.Key, g => g.Count());

        summary.TopSlow = list
            .OrderByDescending(r => r.DurationMs)
            .Take(5)
            .ToList();

        return summary;
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
        return sorted[lower] + (sorted[upper] - sorted[lower]) * weight;
    }
}

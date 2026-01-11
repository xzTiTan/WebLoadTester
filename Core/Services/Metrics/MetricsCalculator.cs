using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.Metrics;

public static class MetricsCalculator
{
    public static MetricsSummary Calculate(IEnumerable<ResultItem> results, int top = 5)
    {
        var list = results.ToList();
        if (list.Count == 0)
        {
            return new MetricsSummary();
        }

        var durations = list.Select(r => (double)r.DurationMs).OrderBy(v => v).ToArray();
        var average = durations.Average();
        var min = durations.First();
        var max = durations.Last();

        var summary = new MetricsSummary
        {
            AverageMs = average,
            MinMs = min,
            MaxMs = max,
            P50Ms = Percentile(durations, 0.50),
            P95Ms = Percentile(durations, 0.95),
            P99Ms = Percentile(durations, 0.99),
            ErrorBreakdown = list.Where(r => !r.Success)
                .GroupBy(r => r.ErrorType ?? r.ErrorMessage ?? "Error")
                .ToDictionary(g => g.Key, g => g.Count()),
            TopSlow = list.OrderByDescending(r => r.DurationMs).Take(top).ToList()
        };

        return summary;
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 0) return 0;
        var position = (sorted.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper) return sorted[lower];
        var weight = position - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }
}

using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.Metrics;

public sealed class MetricsCalculator
{
    public MetricsSummary Calculate(IReadOnlyList<ResultItem> results)
    {
        if (results.Count == 0)
        {
            return new MetricsSummary(0, 0, 0, 0, 0, 0, new Dictionary<string, int>(), new List<ResultItem>());
        }

        var durations = results.Select(r => r.DurationMs).OrderBy(x => x).ToArray();
        var avg = durations.Average();
        var min = durations.First();
        var max = durations.Last();
        var p50 = Percentile(durations, 0.50);
        var p95 = Percentile(durations, 0.95);
        var p99 = Percentile(durations, 0.99);

        var errors = results
            .Where(r => !r.Success)
            .GroupBy(r => r.ErrorType ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        var slowest = results.OrderByDescending(r => r.DurationMs).Take(5).ToList();

        return new MetricsSummary(avg, min, max, p50, p95, p99, errors, slowest);
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        var position = (sorted.Length - 1) * percentile;
        var left = (int)Math.Floor(position);
        var right = (int)Math.Ceiling(position);
        if (left == right)
        {
            return sorted[left];
        }

        var weight = position - left;
        return sorted[left] + (sorted[right] - sorted[left]) * weight;
    }
}

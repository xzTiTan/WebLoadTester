using System.Collections.Generic;

namespace WebLoadTester.Core.Domain;

public sealed record MetricsSummary(
    double MinMs,
    double MaxMs,
    double AvgMs,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    IReadOnlyDictionary<string, int> ErrorBreakdown,
    IReadOnlyList<TestResult> TopSlow);

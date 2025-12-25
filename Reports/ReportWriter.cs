using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain;

namespace WebLoadTester.Reports;

public class ReportWriter
{
    public async Task<string> WriteAsync(TestType type, RunSettings settings, List<RunResult> runs, DateTime startedAt, DateTime finishedAt, CancellationToken ct)
    {
        var doc = new ReportDocument
        {
            TestType = type,
            Settings = settings,
            Runs = runs,
            Meta = new ReportMeta
            {
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                AppVersion = typeof(ReportWriter).Assembly.GetName().Version?.ToString() ?? "1.0"
            },
            Summary = BuildSummary(runs)
        };

        Directory.CreateDirectory("reports");
        var file = Path.Combine("reports", $"report_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json");
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        await using var stream = File.Create(file);
        await JsonSerializer.SerializeAsync(stream, doc, options, ct);
        return file;
    }

    private ReportSummary BuildSummary(List<RunResult> runs)
    {
        var durations = runs.Select(r => r.Duration.TotalMilliseconds).ToList();
        durations.Sort();

        double P(double p)
        {
            if (durations.Count == 0) return 0;
            var index = (int)Math.Ceiling(p * durations.Count) - 1;
            index = Math.Clamp(index, 0, durations.Count - 1);
            return durations[index];
        }

        return new ReportSummary
        {
            TotalRuns = runs.Count,
            Ok = runs.Count(r => r.Success),
            Fail = runs.Count(r => !r.Success),
            AvgDurationMs = durations.Count == 0 ? 0 : durations.Average(),
            MinDurationMs = durations.Count == 0 ? 0 : durations.First(),
            MaxDurationMs = durations.Count == 0 ? 0 : durations.Last(),
            P95DurationMs = P(0.95)
        };
    }
}

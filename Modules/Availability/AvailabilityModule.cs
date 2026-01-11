using System.Diagnostics;
using System.Net.Sockets;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Modules.Availability;

public sealed class AvailabilityModule : ITestModule
{
    public string Id => "net.availability";
    public string DisplayName => "Availability Monitor";
    public TestFamily Family => TestFamily.NetworkSecurity;
    public Type SettingsType => typeof(AvailabilitySettings);

    public object CreateDefaultSettings() => new AvailabilitySettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (AvailabilitySettings)settings;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.Target))
        {
            errors.Add("Target is required.");
        }

        if (s.IntervalSeconds < 5)
        {
            errors.Add("Interval must be >= 5 seconds.");
        }

        if (s.DurationMinutes < 1 || s.DurationMinutes > 30)
        {
            errors.Add("Duration must be between 1 and 30 minutes.");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext context, CancellationToken ct)
    {
        var s = (AvailabilitySettings)settings;
        var report = CreateReportTemplate(context, s);
        var results = new List<ProbeResult>();
        var end = DateTimeOffset.UtcNow.AddMinutes(s.DurationMinutes);
        var failures = 0;

        while (DateTimeOffset.UtcNow < end)
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            var success = await ProbeOnceAsync(s, ct);
            failures = success ? 0 : failures + 1;
            var entry = new ProbeResult
            {
                Kind = "availability",
                Name = $"Check {results.Count + 1}",
                Success = success,
                DurationMs = sw.ElapsedMilliseconds,
                ErrorMessage = success ? null : "Unreachable"
            };
            results.Add(entry);
            context.Progress.Report(results.Count, (int)((end - DateTimeOffset.UtcNow).TotalSeconds / s.IntervalSeconds));

            if (s.FailThreshold.HasValue && failures >= s.FailThreshold.Value)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(s.IntervalSeconds), ct);
        }

        report.Results.AddRange(results);
        report = report with { Metrics = Core.Services.Metrics.MetricsCalculator.Calculate(report.Results) };
        report = report with { Meta = report.Meta with { Status = TestStatus.Completed, FinishedAt = context.Now } };
        return report;
    }

    private static async Task<bool> ProbeOnceAsync(AvailabilitySettings s, CancellationToken ct)
    {
        if (s.TargetType.Equals("Tcp", StringComparison.OrdinalIgnoreCase))
        {
            var parts = s.Target.Split(':', StringSplitOptions.RemoveEmptyEntries);
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 80;
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(s.TimeoutMs);
            await client.ConnectAsync(host, port, cts.Token);
            return client.Connected;
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(s.TimeoutMs) };
        using var response = await httpClient.GetAsync(s.Target, ct);
        return response.IsSuccessStatusCode;
    }

    private static TestReport CreateReportTemplate(IRunContext context, AvailabilitySettings settings)
    {
        return new TestReport
        {
            Meta = new ReportMeta
            {
                Status = TestStatus.Completed,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                AppVersion = typeof(AvailabilityModule).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings)
        };
    }
}

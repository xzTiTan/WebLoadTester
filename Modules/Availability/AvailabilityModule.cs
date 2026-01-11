using System.Diagnostics;
using System.Net.Sockets;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.Availability;

public sealed class AvailabilityModule : ITestModule
{
    private readonly HttpClientProvider _provider = new();

    public string Id => "availability";
    public string DisplayName => "Availability";
    public TestFamily Family => TestFamily.NetSecurity;
    public Type SettingsType => typeof(AvailabilitySettings);

    public object CreateDefaultSettings() => new AvailabilitySettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        if (settings is not AvailabilitySettings s)
        {
            return new[] { "Invalid settings" };
        }

        if (s.IntervalSeconds < 5)
        {
            return new[] { "IntervalSeconds must be >= 5" };
        }

        if (s.DurationMinutes <= 0)
        {
            return new[] { "DurationMinutes must be > 0" };
        }

        return Array.Empty<string>();
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (AvailabilitySettings)settings;
        var start = ctx.Now;
        var results = new List<ResultItem>();
        var endAt = start.AddMinutes(Math.Min(s.DurationMinutes, ctx.Limits.MaxAvailabilityDurationMinutes));
        var total = 0;
        var success = 0;

        while (DateTimeOffset.Now < endAt && !ct.IsCancellationRequested)
        {
            total++;
            var sw = Stopwatch.StartNew();
            var ok = false;
            string? error = null;
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(s.TimeoutMs));
                if (s.TargetType == AvailabilityTargetType.Http)
                {
                    var response = await _provider.Client.GetAsync(s.Target, timeoutCts.Token);
                    ok = response.IsSuccessStatusCode;
                    if (!ok)
                    {
                        error = response.ReasonPhrase;
                    }
                }
                else
                {
                    var parts = s.Target.Split(':');
                    var host = parts[0];
                    var port = parts.Length > 1 && int.TryParse(parts[1], out var parsed) ? parsed : 80;
                    using var client = new TcpClient();
                    await client.ConnectAsync(host, port, timeoutCts.Token);
                    ok = true;
                }
            }
            catch (Exception ex)
            {
                ok = false;
                error = ex.Message;
            }

            sw.Stop();
            if (ok)
            {
                success++;
            }

            results.Add(new ProbeResult
            {
                Kind = "Availability",
                Name = s.Target,
                Target = s.Target,
                Success = ok,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                ErrorMessage = error,
                ErrorType = ok ? null : "Availability"
            });

            ctx.Progress.Report(new ProgressUpdate(total, 0, $"{total} checks"));
            await Task.Delay(TimeSpan.FromSeconds(s.IntervalSeconds), ct);
        }

        var uptime = total == 0 ? 0 : success / (double)total * 100;
        results.Add(new ProbeResult
        {
            Kind = "Summary",
            Name = "Uptime",
            Target = s.Target,
            Success = true,
            DurationMs = 0,
            Data = new Dictionary<string, string> { ["UptimePercent"] = uptime.ToString("F1") }
        });

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = start,
            FinishedAt = ctx.Now,
            Status = ct.IsCancellationRequested ? TestStatus.Stopped : TestStatus.Completed,
            AppVersion = AppVersionProvider.GetVersion(),
            OsDescription = Environment.OSVersion.ToString(),
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings),
            Results = results
        };
    }
}

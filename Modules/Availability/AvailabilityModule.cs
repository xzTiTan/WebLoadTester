using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.Availability;

public sealed class AvailabilityModule : ITestModule
{
    private readonly HttpClientProvider _clientProvider = new();

    public string Id => "availability";
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
            errors.Add("IntervalSeconds must be >= 5.");
        }
        if (s.DurationMinutes <= 0)
        {
            errors.Add("DurationMinutes must be positive.");
        }
        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (AvailabilitySettings)settings;
        var results = new List<TestResult>();
        var end = DateTimeOffset.Now.AddMinutes(s.DurationMinutes);
        var totalChecks = Math.Max(1, (int)Math.Ceiling((end - DateTimeOffset.Now).TotalSeconds / s.IntervalSeconds));
        var completed = 0;
        var client = _clientProvider.Client;
        client.Timeout = TimeSpan.FromMilliseconds(s.TimeoutMs);

        while (DateTimeOffset.Now < end)
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            try
            {
                if (s.TargetType == AvailabilityTargetType.Http)
                {
                    using var response = await client.GetAsync(s.Target, ct).ConfigureAwait(false);
                    sw.Stop();
                    var success = response.IsSuccessStatusCode;
                    results.Add(new ProbeResult($"HTTP {s.Target}", success, success ? null : $"HTTP {(int)response.StatusCode}", sw.Elapsed.TotalMilliseconds, null));
                }
                else
                {
                    var parts = s.Target.Split(':');
                    var host = parts[0];
                    var port = parts.Length > 1 && int.TryParse(parts[1], out var parsed) ? parsed : 80;
                    using var clientTcp = new TcpClient();
                    await clientTcp.ConnectAsync(host, port, ct).ConfigureAwait(false);
                    sw.Stop();
                    results.Add(new ProbeResult($"TCP {host}:{port}", true, null, sw.Elapsed.TotalMilliseconds, null));
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new ProbeResult($"Check {s.Target}", false, ex.Message, sw.Elapsed.TotalMilliseconds, ex.Message));
            }
            finally
            {
                completed++;
                ctx.Progress.Report(new ProgressUpdate(completed, totalChecks));
            }

            await Task.Delay(TimeSpan.FromSeconds(s.IntervalSeconds), ct).ConfigureAwait(false);
        }

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            FinishedAt = ctx.Now,
            Status = TestStatus.Completed,
            Results = results
        };
    }
}

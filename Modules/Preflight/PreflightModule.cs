using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.Preflight;

public sealed class PreflightModule : ITestModule
{
    private readonly HttpClientProvider _clientProvider = new();

    public string Id => "preflight";
    public string DisplayName => "Preflight";
    public TestFamily Family => TestFamily.NetworkSecurity;
    public Type SettingsType => typeof(PreflightSettings);

    public object CreateDefaultSettings() => new PreflightSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (PreflightSettings)settings;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.Target))
        {
            errors.Add("Target is required.");
        }
        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (PreflightSettings)settings;
        var results = new List<TestResult>();
        var uri = new Uri(s.Target);

        if (s.CheckDns)
        {
            try
            {
                var (duration, _) = await NetworkProbes.DnsResolveAsync(uri.Host, ct).ConfigureAwait(false);
                results.Add(new ProbeResult("DNS", true, null, duration.TotalMilliseconds, null));
            }
            catch (Exception ex)
            {
                results.Add(new ProbeResult("DNS", false, ex.Message, 0, ex.Message));
            }
        }

        if (s.CheckTcp)
        {
            try
            {
                var duration = await NetworkProbes.TcpConnectAsync(uri.Host, uri.Port, ct).ConfigureAwait(false);
                results.Add(new ProbeResult("TCP", true, null, duration.TotalMilliseconds, null));
            }
            catch (Exception ex)
            {
                results.Add(new ProbeResult("TCP", false, ex.Message, 0, ex.Message));
            }
        }

        if (s.CheckTls)
        {
            try
            {
                var (duration, days) = await NetworkProbes.TlsHandshakeAsync(uri.Host, 443, ct).ConfigureAwait(false);
                results.Add(new ProbeResult("TLS", true, null, duration.TotalMilliseconds, $"Days to expiry: {days}"));
            }
            catch (Exception ex)
            {
                results.Add(new ProbeResult("TLS", false, ex.Message, 0, ex.Message));
            }
        }

        if (s.CheckHttp)
        {
            try
            {
                var client = _clientProvider.Client;
                using var response = await client.GetAsync(s.Target, ct).ConfigureAwait(false);
                results.Add(new CheckResult("HTTP", response.IsSuccessStatusCode, response.IsSuccessStatusCode ? null : "HTTP error", 0, (int)response.StatusCode, null));
            }
            catch (Exception ex)
            {
                results.Add(new CheckResult("HTTP", false, ex.Message, 0, null, ex.Message));
            }
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

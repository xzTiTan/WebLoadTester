using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.NetDiagnostics;

public sealed class NetDiagnosticsModule : ITestModule
{
    public string Id => "net-diagnostics";
    public string DisplayName => "Network Diagnostics";
    public TestFamily Family => TestFamily.NetworkSecurity;
    public Type SettingsType => typeof(NetDiagnosticsSettings);

    public object CreateDefaultSettings() => new NetDiagnosticsSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (NetDiagnosticsSettings)settings;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.Hostname))
        {
            errors.Add("Hostname is required.");
        }
        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (NetDiagnosticsSettings)settings;
        var results = new List<TestResult>();
        var total = (s.EnableDns ? 1 : 0) + (s.EnableTcp ? s.Ports.Count : 0) + (s.EnableTls ? 1 : 0);
        var completed = 0;

        if (s.EnableDns)
        {
            try
            {
                var (duration, addresses) = await NetworkProbes.DnsResolveAsync(s.Hostname, ct).ConfigureAwait(false);
                results.Add(new ProbeResult("DNS Resolve", true, null, duration.TotalMilliseconds, string.Join(",", addresses)));
            }
            catch (Exception ex)
            {
                results.Add(new ProbeResult("DNS Resolve", false, ex.Message, 0, ex.Message));
            }
            finally
            {
                ctx.Progress.Report(new ProgressUpdate(++completed, total));
            }
        }

        if (s.EnableTcp)
        {
            foreach (var port in s.Ports)
            {
                try
                {
                    var duration = await NetworkProbes.TcpConnectAsync(s.Hostname, port, ct).ConfigureAwait(false);
                    results.Add(new ProbeResult($"TCP {port}", true, null, duration.TotalMilliseconds, null));
                }
                catch (Exception ex)
                {
                    results.Add(new ProbeResult($"TCP {port}", false, ex.Message, 0, ex.Message));
                }
                finally
                {
                    ctx.Progress.Report(new ProgressUpdate(++completed, total));
                }
            }
        }

        if (s.EnableTls)
        {
            try
            {
                var (duration, days) = await NetworkProbes.TlsHandshakeAsync(s.Hostname, 443, ct).ConfigureAwait(false);
                results.Add(new ProbeResult("TLS Handshake", true, null, duration.TotalMilliseconds, $"Days to expiry: {days}"));
            }
            catch (Exception ex)
            {
                results.Add(new ProbeResult("TLS Handshake", false, ex.Message, 0, ex.Message));
            }
            finally
            {
                ctx.Progress.Report(new ProgressUpdate(++completed, total));
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

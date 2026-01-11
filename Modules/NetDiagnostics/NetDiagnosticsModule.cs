using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.NetDiagnostics;

public class NetDiagnosticsModule : ITestModule
{
    public string Id => "net.diagnostics";
    public string DisplayName => "Network Diagnostics";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(NetDiagnosticsSettings);

    public object CreateDefaultSettings() => new NetDiagnosticsSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not NetDiagnosticsSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(s.Hostname))
        {
            errors.Add("Hostname required");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (NetDiagnosticsSettings)settings;
        var report = new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Completed,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            AppVersion = typeof(NetDiagnosticsModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };

        var results = new List<ResultBase>();

        if (s.EnableDns)
        {
            var (success, duration, details) = await NetworkProbes.DnsProbeAsync(s.Hostname, ct);
            results.Add(new ProbeResult("DNS")
            {
                Success = success,
                DurationMs = duration,
                Details = details,
                ErrorType = success ? null : "DNS",
                ErrorMessage = success ? null : details
            });
        }

        if (s.EnableTcp)
        {
            foreach (var port in s.Ports)
            {
                var (success, duration, details) = await NetworkProbes.TcpProbeAsync(s.Hostname, port, ct);
                results.Add(new ProbeResult($"TCP:{port}")
                {
                    Success = success,
                    DurationMs = duration,
                    Details = details,
                    ErrorType = success ? null : "TCP",
                    ErrorMessage = success ? null : details
                });
            }
        }

        if (s.EnableTls)
        {
            var (success, duration, details, daysToExpiry) = await NetworkProbes.TlsProbeAsync(s.Hostname, 443, ct);
            results.Add(new ProbeResult("TLS")
            {
                Success = success,
                DurationMs = duration,
                Details = daysToExpiry.HasValue ? $"{details} (Days to expiry: {daysToExpiry})" : details,
                ErrorType = success ? null : "TLS",
                ErrorMessage = success ? null : details
            });
        }

        report.Results = results;
        report.FinishedAt = ctx.Now;
        return report;
    }
}

using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.NetDiagnostics;

public sealed class NetDiagnosticsModule : ITestModule
{
    public string Id => "net-diagnostics";
    public string DisplayName => "Network Diagnostics";
    public TestFamily Family => TestFamily.NetSecurity;
    public Type SettingsType => typeof(NetDiagnosticsSettings);

    public object CreateDefaultSettings() => new NetDiagnosticsSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        if (settings is not NetDiagnosticsSettings s)
        {
            return new[] { "Invalid settings" };
        }

        if (string.IsNullOrWhiteSpace(s.Hostname))
        {
            return new[] { "Hostname is required" };
        }

        return Array.Empty<string>();
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (NetDiagnosticsSettings)settings;
        var start = ctx.Now;
        var results = new List<ResultItem>();

        if (s.EnableDns)
        {
            var dns = await DnsProbe.ResolveAsync(s.Hostname, ct);
            results.Add(new ProbeResult
            {
                Kind = "DNS",
                Name = "DNS Resolve",
                Target = s.Hostname,
                Success = dns.Success,
                DurationMs = dns.DurationMs,
                ErrorMessage = dns.Error,
                ErrorType = dns.Success ? null : "Dns"
            });
        }

        if (s.EnableTcp)
        {
            foreach (var port in s.Ports)
            {
                var tcp = await TcpProbe.ConnectAsync(s.Hostname, port, ct);
                results.Add(new ProbeResult
                {
                    Kind = "TCP",
                    Name = $"TCP {port}",
                    Target = s.Hostname,
                    Success = tcp.Success,
                    DurationMs = tcp.DurationMs,
                    ErrorMessage = tcp.Error,
                    ErrorType = tcp.Success ? null : "Tcp"
                });
            }
        }

        if (s.EnableTls)
        {
            var tls = await TlsProbe.HandshakeAsync(s.Hostname, 443, ct);
            results.Add(new ProbeResult
            {
                Kind = "TLS",
                Name = "TLS 443",
                Target = s.Hostname,
                Success = tls.Success,
                DurationMs = tls.DurationMs,
                ErrorMessage = tls.Error,
                ErrorType = tls.Success ? null : "Tls",
                Data = new Dictionary<string, string> { ["DaysToExpiry"] = tls.DaysToExpiry.ToString() }
            });
        }

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = start,
            FinishedAt = ctx.Now,
            Status = TestStatus.Completed,
            AppVersion = AppVersionProvider.GetVersion(),
            OsDescription = Environment.OSVersion.ToString(),
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings),
            Results = results
        };
    }
}

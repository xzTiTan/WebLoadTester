using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.NetDiagnostics;

public sealed class NetDiagnosticsModule : ITestModule
{
    private readonly DnsProbe _dnsProbe = new();
    private readonly TcpProbe _tcpProbe = new();
    private readonly TlsProbe _tlsProbe = new();

    public string Id => "net.diagnostics";
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

    public async Task<TestReport> RunAsync(object settings, IRunContext context, CancellationToken ct)
    {
        var s = (NetDiagnosticsSettings)settings;
        var report = CreateReportTemplate(context, s);
        var results = new List<ProbeResult>();

        if (s.EnableDns)
        {
            var dns = await _dnsProbe.ResolveAsync(s.Hostname, ct);
            results.Add(new ProbeResult
            {
                Kind = "dns",
                Name = "DNS Resolve",
                Success = dns.Success,
                DurationMs = dns.DurationMs,
                Target = s.Hostname,
                ErrorMessage = dns.Error
            });
        }

        if (s.EnableTcp)
        {
            foreach (var port in s.Ports)
            {
                var tcp = await _tcpProbe.ConnectAsync(s.Hostname, port, ct);
                results.Add(new ProbeResult
                {
                    Kind = "tcp",
                    Name = $"TCP {port}",
                    Success = tcp.Success,
                    DurationMs = tcp.DurationMs,
                    Target = $"{s.Hostname}:{port}",
                    ErrorMessage = tcp.Error
                });
            }
        }

        if (s.EnableTls)
        {
            var tls = await _tlsProbe.HandshakeAsync(s.Hostname, 443, ct);
            results.Add(new ProbeResult
            {
                Kind = "tls",
                Name = "TLS Handshake",
                Success = tls.Success,
                DurationMs = tls.DurationMs,
                Target = s.Hostname,
                ErrorMessage = tls.Error,
                ErrorType = tls.DaysToExpiry.HasValue ? $"DaysToExpiry={tls.DaysToExpiry}" : null
            });
        }

        report.Results.AddRange(results);
        report = report with { Metrics = Core.Services.Metrics.MetricsCalculator.Calculate(report.Results) };
        report = report with { Meta = report.Meta with { Status = TestStatus.Completed, FinishedAt = context.Now } };
        return report;
    }

    private static TestReport CreateReportTemplate(IRunContext context, NetDiagnosticsSettings settings)
    {
        return new TestReport
        {
            Meta = new ReportMeta
            {
                Status = TestStatus.Completed,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                AppVersion = typeof(NetDiagnosticsModule).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings)
        };
    }
}

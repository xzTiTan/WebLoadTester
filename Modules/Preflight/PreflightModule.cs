using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.Preflight;

public sealed class PreflightModule : ITestModule
{
    private readonly DnsProbe _dnsProbe = new();
    private readonly TcpProbe _tcpProbe = new();
    private readonly TlsProbe _tlsProbe = new();

    public string Id => "net.preflight";
    public string DisplayName => "Preflight Checks";
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

    public async Task<TestReport> RunAsync(object settings, IRunContext context, CancellationToken ct)
    {
        var s = (PreflightSettings)settings;
        var report = CreateReportTemplate(context, s);
        var results = new List<ProbeResult>();
        var host = s.Target.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(s.Target).Host
            : s.Target;

        if (s.CheckDns)
        {
            var dns = await _dnsProbe.ResolveAsync(host, ct);
            results.Add(new ProbeResult
            {
                Kind = "dns",
                Name = "DNS",
                Success = dns.Success,
                DurationMs = dns.DurationMs,
                Target = host,
                ErrorMessage = dns.Error
            });
        }

        if (s.CheckTcp)
        {
            var tcp = await _tcpProbe.ConnectAsync(host, 443, ct);
            results.Add(new ProbeResult
            {
                Kind = "tcp",
                Name = "TCP 443",
                Success = tcp.Success,
                DurationMs = tcp.DurationMs,
                Target = host,
                ErrorMessage = tcp.Error
            });
        }

        if (s.CheckTls)
        {
            var tls = await _tlsProbe.HandshakeAsync(host, 443, ct);
            results.Add(new ProbeResult
            {
                Kind = "tls",
                Name = "TLS",
                Success = tls.Success,
                DurationMs = tls.DurationMs,
                Target = host,
                ErrorMessage = tls.Error,
                ErrorType = tls.DaysToExpiry.HasValue ? $"DaysToExpiry={tls.DaysToExpiry}" : null
            });
        }

        if (s.CheckHttp)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            try
            {
                using var response = await client.GetAsync(s.Target, ct);
                results.Add(new ProbeResult
                {
                    Kind = "http",
                    Name = "HTTP",
                    Success = response.IsSuccessStatusCode,
                    DurationMs = 0,
                    Target = s.Target,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = response.IsSuccessStatusCode ? null : "HTTP error"
                });
            }
            catch (Exception ex)
            {
                results.Add(new ProbeResult
                {
                    Kind = "http",
                    Name = "HTTP",
                    Success = false,
                    DurationMs = 0,
                    Target = s.Target,
                    ErrorMessage = ex.Message,
                    ErrorType = ex.GetType().Name
                });
            }
        }

        report.Results.AddRange(results);
        report = report with { Metrics = Core.Services.Metrics.MetricsCalculator.Calculate(report.Results) };
        report = report with { Meta = report.Meta with { Status = TestStatus.Completed, FinishedAt = context.Now } };
        return report;
    }

    private static TestReport CreateReportTemplate(IRunContext context, PreflightSettings settings)
    {
        return new TestReport
        {
            Meta = new ReportMeta
            {
                Status = TestStatus.Completed,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                AppVersion = typeof(PreflightModule).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings)
        };
    }
}

using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Http;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.Preflight;

public sealed class PreflightModule : ITestModule
{
    private readonly HttpClientProvider _provider = new();

    public string Id => "preflight";
    public string DisplayName => "Preflight";
    public TestFamily Family => TestFamily.NetSecurity;
    public Type SettingsType => typeof(PreflightSettings);

    public object CreateDefaultSettings() => new PreflightSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        if (settings is not PreflightSettings s)
        {
            return new[] { "Invalid settings" };
        }

        if (string.IsNullOrWhiteSpace(s.Target))
        {
            return new[] { "Target is required" };
        }

        return Array.Empty<string>();
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (PreflightSettings)settings;
        var start = ctx.Now;
        var results = new List<ResultItem>();
        var uri = s.Target.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? new Uri(s.Target) : new Uri($"https://{s.Target}");
        var host = uri.Host;

        if (s.CheckDns)
        {
            var dns = await DnsProbe.ResolveAsync(host, ct);
            results.Add(new ProbeResult
            {
                Kind = "DNS",
                Name = "DNS",
                Target = host,
                Success = dns.Success,
                DurationMs = dns.DurationMs,
                ErrorMessage = dns.Error,
                ErrorType = dns.Success ? null : "Dns"
            });
        }

        if (s.CheckTcp)
        {
            var tcp = await TcpProbe.ConnectAsync(host, 443, ct);
            results.Add(new ProbeResult
            {
                Kind = "TCP",
                Name = "TCP",
                Target = host,
                Success = tcp.Success,
                DurationMs = tcp.DurationMs,
                ErrorMessage = tcp.Error,
                ErrorType = tcp.Success ? null : "Tcp"
            });
        }

        if (s.CheckTls)
        {
            var tls = await TlsProbe.HandshakeAsync(host, 443, ct);
            results.Add(new ProbeResult
            {
                Kind = "TLS",
                Name = "TLS",
                Target = host,
                Success = tls.Success,
                DurationMs = tls.DurationMs,
                ErrorMessage = tls.Error,
                ErrorType = tls.Success ? null : "Tls",
                Data = new Dictionary<string, string> { ["DaysToExpiry"] = tls.DaysToExpiry.ToString() }
            });
        }

        if (s.CheckHttp)
        {
            try
            {
                var response = await _provider.Client.GetAsync(uri, ct);
                results.Add(new ProbeResult
                {
                    Kind = "HTTP",
                    Name = "HTTP",
                    Target = uri.ToString(),
                    Success = response.IsSuccessStatusCode,
                    DurationMs = 0,
                    ErrorMessage = response.IsSuccessStatusCode ? null : response.ReasonPhrase,
                    ErrorType = response.IsSuccessStatusCode ? null : "Http"
                });
            }
            catch (Exception ex)
            {
                results.Add(new ProbeResult
                {
                    Kind = "HTTP",
                    Name = "HTTP",
                    Target = uri.ToString(),
                    Success = false,
                    DurationMs = 0,
                    ErrorMessage = ex.Message,
                    ErrorType = ex.GetType().Name
                });
            }
        }

        var allOk = results.All(r => r.Success);
        results.Add(new ProbeResult
        {
            Kind = "Summary",
            Name = "Ready",
            Target = host,
            Success = allOk,
            DurationMs = 0,
            ErrorMessage = allOk ? null : "Not ready",
            ErrorType = allOk ? null : "Preflight"
        });

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

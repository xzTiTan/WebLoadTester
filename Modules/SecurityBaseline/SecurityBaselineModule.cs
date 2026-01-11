using System.Net;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Http;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.SecurityBaseline;

public sealed class SecurityBaselineModule : ITestModule
{
    private readonly HttpClientProvider _provider = new();

    public string Id => "security-baseline";
    public string DisplayName => "Security Baseline";
    public TestFamily Family => TestFamily.NetSecurity;
    public Type SettingsType => typeof(SecurityBaselineSettings);

    public object CreateDefaultSettings() => new SecurityBaselineSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        if (settings is not SecurityBaselineSettings s)
        {
            return new[] { "Invalid settings" };
        }

        if (string.IsNullOrWhiteSpace(s.Url))
        {
            return new[] { "Url is required" };
        }

        return Array.Empty<string>();
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (SecurityBaselineSettings)settings;
        var start = ctx.Now;
        var results = new List<ResultItem>();

        var response = await _provider.Client.GetAsync(s.Url, ct);
        results.Add(HeaderCheck(response.Headers.Contains("Strict-Transport-Security"), "HSTS"));
        results.Add(HeaderCheck(response.Headers.Contains("X-Frame-Options"), "X-Frame-Options"));
        results.Add(HeaderCheck(response.Headers.Contains("X-Content-Type-Options"), "X-Content-Type-Options"));
        results.Add(HeaderCheck(response.Headers.Contains("Content-Security-Policy"), "Content-Security-Policy"));

        if (s.CheckRedirectHttpToHttps)
        {
            var httpUrl = s.Url.Replace("https://", "http://", StringComparison.OrdinalIgnoreCase);
            try
            {
                var httpResponse = await _provider.Client.GetAsync(httpUrl, ct);
                var ok = httpResponse.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
                results.Add(new ProbeResult
                {
                    Kind = "Redirect",
                    Name = "HTTP to HTTPS",
                    Target = httpUrl,
                    Success = ok,
                    DurationMs = 0,
                    ErrorMessage = ok ? null : "No redirect",
                    ErrorType = ok ? null : "Redirect"
                });
            }
            catch (Exception ex)
            {
                results.Add(new ProbeResult
                {
                    Kind = "Redirect",
                    Name = "HTTP to HTTPS",
                    Target = httpUrl,
                    Success = false,
                    DurationMs = 0,
                    ErrorMessage = ex.Message,
                    ErrorType = ex.GetType().Name
                });
            }
        }

        if (s.CheckTlsExpiry)
        {
            var host = new Uri(s.Url).Host;
            var tls = await TlsProbe.HandshakeAsync(host, 443, ct);
            results.Add(new ProbeResult
            {
                Kind = "TLS",
                Name = "TLS Expiry",
                Target = host,
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

    private static ProbeResult HeaderCheck(bool present, string name)
    {
        return new ProbeResult
        {
            Kind = "Header",
            Name = name,
            Target = name,
            Success = present,
            DurationMs = 0,
            ErrorMessage = present ? null : "Missing",
            ErrorType = present ? null : "Header"
        };
    }
}

using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.SecurityBaseline;

public sealed class SecurityBaselineModule : ITestModule
{
    private readonly TlsProbe _tlsProbe = new();

    public string Id => "net.security";
    public string DisplayName => "Security Baseline";
    public TestFamily Family => TestFamily.NetworkSecurity;
    public Type SettingsType => typeof(SecurityBaselineSettings);

    public object CreateDefaultSettings() => new SecurityBaselineSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (SecurityBaselineSettings)settings;
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.Url))
        {
            errors.Add("URL is required.");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext context, CancellationToken ct)
    {
        var s = (SecurityBaselineSettings)settings;
        var report = CreateReportTemplate(context, s);
        var results = new List<CheckResult>();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var response = await client.GetAsync(s.Url, ct);
        if (s.CheckHeaders)
        {
            results.Add(CheckHeader(response, "Strict-Transport-Security"));
            results.Add(CheckHeader(response, "X-Frame-Options"));
            results.Add(CheckHeader(response, "X-Content-Type-Options"));
            results.Add(CheckHeader(response, "Content-Security-Policy"));
        }

        if (s.CheckRedirectHttpToHttps && s.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var httpUrl = "http://" + s.Url.Substring("https://".Length);
            using var httpResponse = await client.GetAsync(httpUrl, ct);
            var success = httpResponse.StatusCode is System.Net.HttpStatusCode.MovedPermanently or System.Net.HttpStatusCode.Redirect;
            results.Add(new CheckResult
            {
                Kind = "redirect",
                Name = "HTTP to HTTPS",
                Success = success,
                DurationMs = 0,
                StatusCode = (int)httpResponse.StatusCode,
                ErrorMessage = success ? null : "No redirect"
            });
        }

        if (s.CheckTlsExpiry)
        {
            var host = new Uri(s.Url).Host;
            var tls = await _tlsProbe.HandshakeAsync(host, 443, ct);
            results.Add(new CheckResult
            {
                Kind = "tls-expiry",
                Name = "TLS Expiry",
                Success = tls.Success,
                DurationMs = tls.DurationMs,
                ErrorMessage = tls.Error,
                ErrorType = tls.DaysToExpiry.HasValue ? $"DaysToExpiry={tls.DaysToExpiry}" : null
            });
        }

        report.Results.AddRange(results);
        report = report with { Metrics = Core.Services.Metrics.MetricsCalculator.Calculate(report.Results) };
        report = report with { Meta = report.Meta with { Status = TestStatus.Completed, FinishedAt = context.Now } };
        return report;
    }

    private static CheckResult CheckHeader(HttpResponseMessage response, string header)
    {
        var exists = response.Headers.Contains(header);
        return new CheckResult
        {
            Kind = "header",
            Name = header,
            Success = exists,
            DurationMs = 0,
            ErrorMessage = exists ? null : "Missing"
        };
    }

    private static TestReport CreateReportTemplate(IRunContext context, SecurityBaselineSettings settings)
    {
        return new TestReport
        {
            Meta = new ReportMeta
            {
                Status = TestStatus.Completed,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                AppVersion = typeof(SecurityBaselineModule).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings)
        };
    }
}

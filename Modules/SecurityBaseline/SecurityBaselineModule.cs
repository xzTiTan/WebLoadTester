using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.SecurityBaseline;

public class SecurityBaselineModule : ITestModule
{
    public string Id => "net.security";
    public string DisplayName => "Security Baseline";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(SecurityBaselineSettings);

    public object CreateDefaultSettings() => new SecurityBaselineSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not SecurityBaselineSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(s.Url))
        {
            errors.Add("Url required");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (SecurityBaselineSettings)settings;
        var report = new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Completed,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            AppVersion = typeof(SecurityBaselineModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };

        using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(10));
        var results = new List<ResultBase>();

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, s.Url), ct);
        var headers = response.Headers;

        if (s.CheckHeaders)
        {
            var requiredHeaders = new[] { "Strict-Transport-Security", "X-Frame-Options", "X-Content-Type-Options", "Content-Security-Policy" };
            foreach (var header in requiredHeaders)
            {
                var present = headers.Contains(header);
                results.Add(new CheckResult(header)
                {
                    Success = present,
                    DurationMs = 0,
                    ErrorType = present ? null : "Header",
                    ErrorMessage = present ? null : "Missing"
                });
            }
        }

        if (s.CheckRedirectHttpToHttps)
        {
            var httpUrl = s.Url.Replace("https://", "http://", StringComparison.OrdinalIgnoreCase);
            var httpResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, httpUrl), ct);
            var redirect = (int)httpResponse.StatusCode >= 300 && (int)httpResponse.StatusCode < 400;
            results.Add(new CheckResult("HTTP->HTTPS")
            {
                Success = redirect,
                DurationMs = 0,
                ErrorType = redirect ? null : "Redirect",
                ErrorMessage = redirect ? null : "No redirect"
            });
        }

        if (s.CheckTlsExpiry)
        {
            var host = new Uri(s.Url).Host;
            var (success, duration, details, days) = await NetworkProbes.TlsProbeAsync(host, 443, ct);
            results.Add(new ProbeResult("TLS Expiry")
            {
                Success = success,
                DurationMs = duration,
                Details = days.HasValue ? $"Days to expiry: {days}" : details,
                ErrorType = success ? null : "TLS",
                ErrorMessage = success ? null : details
            });
        }

        report.Results = results;
        report.FinishedAt = ctx.Now;
        return report;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.SecurityBaseline;

public sealed class SecurityBaselineModule : ITestModule
{
    private readonly HttpClientProvider _clientProvider = new();

    public string Id => "security-baseline";
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
            errors.Add("Url is required.");
        }
        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (SecurityBaselineSettings)settings;
        var results = new List<TestResult>();
        var client = _clientProvider.Client;

        if (s.CheckHeaders)
        {
            try
            {
                using var response = await client.GetAsync(s.Url, ct).ConfigureAwait(false);
                var missing = new List<string>();
                RequireHeader(response, "Strict-Transport-Security", missing);
                RequireHeader(response, "X-Frame-Options", missing);
                RequireHeader(response, "X-Content-Type-Options", missing);
                RequireHeader(response, "Content-Security-Policy", missing);
                if (missing.Count == 0)
                {
                    results.Add(new CheckResult("Security headers", true, null, 0, (int)response.StatusCode, null));
                }
                else
                {
                    results.Add(new CheckResult("Security headers", false, string.Join(", ", missing), 0, (int)response.StatusCode, string.Join(", ", missing)));
                }
            }
            catch (Exception ex)
            {
                results.Add(new CheckResult("Security headers", false, ex.Message, 0, null, ex.Message));
            }
        }

        if (s.CheckRedirectHttpToHttps && s.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var httpUrl = "http://" + s.Url.Substring("https://".Length);
                using var response = await client.GetAsync(httpUrl, ct).ConfigureAwait(false);
                var isRedirect = (int)response.StatusCode >= 300 && (int)response.StatusCode < 400;
                results.Add(new CheckResult("HTTP->HTTPS redirect", isRedirect, isRedirect ? null : "No redirect", 0, (int)response.StatusCode, null));
            }
            catch (Exception ex)
            {
                results.Add(new CheckResult("HTTP->HTTPS redirect", false, ex.Message, 0, null, ex.Message));
            }
        }

        if (s.CheckTlsExpiry)
        {
            try
            {
                var host = new Uri(s.Url).Host;
                var (duration, days) = await NetworkProbes.TlsHandshakeAsync(host, 443, ct).ConfigureAwait(false);
                var ok = days > 7;
                results.Add(new ProbeResult("TLS expiry", ok, ok ? null : $"Certificate expires in {days} days", duration.TotalMilliseconds, $"Days to expiry: {days}"));
            }
            catch (Exception ex)
            {
                results.Add(new ProbeResult("TLS expiry", false, ex.Message, 0, ex.Message));
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

    private static void RequireHeader(HttpResponseMessage response, string name, List<string> missing)
    {
        if (!response.Headers.Contains(name) && !response.Content.Headers.Contains(name))
        {
            missing.Add(name);
        }
    }
}

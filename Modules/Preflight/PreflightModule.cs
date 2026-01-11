using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.Preflight;

public class PreflightModule : ITestModule
{
    public string Id => "net.preflight";
    public string DisplayName => "Preflight";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(PreflightSettings);

    public object CreateDefaultSettings() => new PreflightSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not PreflightSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(s.Target))
        {
            errors.Add("Target required");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (PreflightSettings)settings;
        var report = new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Completed,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            AppVersion = typeof(PreflightModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };

        var results = new List<ResultBase>();
        var targetUri = new Uri(s.Target);

        if (s.CheckDns)
        {
            var (success, duration, details) = await NetworkProbes.DnsProbeAsync(targetUri.Host, ct);
            results.Add(new ProbeResult("DNS")
            {
                Success = success,
                DurationMs = duration,
                Details = details,
                ErrorType = success ? null : "DNS",
                ErrorMessage = success ? null : details
            });
        }

        if (s.CheckTcp)
        {
            var port = targetUri.Port == -1 ? (targetUri.Scheme == "https" ? 443 : 80) : targetUri.Port;
            var (success, duration, details) = await NetworkProbes.TcpProbeAsync(targetUri.Host, port, ct);
            results.Add(new ProbeResult("TCP")
            {
                Success = success,
                DurationMs = duration,
                Details = details,
                ErrorType = success ? null : "TCP",
                ErrorMessage = success ? null : details
            });
        }

        if (s.CheckTls && targetUri.Scheme == "https")
        {
            var (success, duration, details, days) = await NetworkProbes.TlsProbeAsync(targetUri.Host, 443, ct);
            results.Add(new ProbeResult("TLS")
            {
                Success = success,
                DurationMs = duration,
                Details = days.HasValue ? $"Days to expiry: {days}" : details,
                ErrorType = success ? null : "TLS",
                ErrorMessage = success ? null : details
            });
        }

        if (s.CheckHttp)
        {
            using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(5));
            try
            {
                var response = await client.GetAsync(s.Target, ct);
                results.Add(new CheckResult("HTTP")
                {
                    Success = response.IsSuccessStatusCode,
                    DurationMs = 0,
                    StatusCode = (int)response.StatusCode,
                    ErrorType = response.IsSuccessStatusCode ? null : "HTTP",
                    ErrorMessage = response.IsSuccessStatusCode ? null : response.StatusCode.ToString()
                });
            }
            catch (Exception ex)
            {
                results.Add(new CheckResult("HTTP")
                {
                    Success = false,
                    DurationMs = 0,
                    ErrorType = ex.GetType().Name,
                    ErrorMessage = ex.Message
                });
            }
        }

        report.Results = results;
        report.FinishedAt = ctx.Now;
        return report;
    }
}

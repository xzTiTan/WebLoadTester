using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.Preflight;

/// <summary>
/// Модуль предварительных сетевых и HTTP-проверок.
/// </summary>
public class PreflightModule : ITestModule
{
    public string Id => "net.preflight";
    public string DisplayName => "Предварительные проверки";
    public string Description => "Быстро проверяет готовность цели (DNS/TCP/TLS/HTTP) перед основным запуском.";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(PreflightSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию.
    /// </summary>
    public object CreateDefaultSettings() => new PreflightSettings();

    /// <summary>
    /// Проверяет корректность настроек цели.
    /// </summary>
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

    /// <summary>
    /// Выполняет серию preflight-проверок и формирует результат.
    /// </summary>
    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (PreflightSettings)settings;
        var result = new ModuleResult();
        var normalizedTarget = NormalizeTarget(s.Target);
        var totalChecks = (s.CheckDns ? 1 : 0) + (s.CheckTcp ? 1 : 0) + (s.CheckTls ? 1 : 0) + (s.CheckHttp ? 1 : 0);
        var current = 0;
        ctx.Progress.Report(new ProgressUpdate(0, Math.Max(totalChecks, 1), "Preflight старт"));
        ctx.Log.Info($"[Preflight] Target={normalizedTarget}");

        var results = new List<ResultBase>();
        var targetUri = new Uri(normalizedTarget);

        if (s.CheckDns)
        {
            var (success, duration, details) = await NetworkProbes.DnsProbeAsync(targetUri.Host, ct);
            results.Add(new PreflightResult("DNS")
            {
                Success = success,
                DurationMs = duration,
                Details = details,
                ErrorType = success ? null : "DNS",
                ErrorMessage = success ? null : details
            });
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "Preflight DNS"));
        }

        if (s.CheckTcp)
        {
            var port = targetUri.Port == -1 ? (targetUri.Scheme == "https" ? 443 : 80) : targetUri.Port;
            var (success, duration, details) = await NetworkProbes.TcpProbeAsync(targetUri.Host, port, ct);
            results.Add(new PreflightResult("TCP")
            {
                Success = success,
                DurationMs = duration,
                Details = details,
                ErrorType = success ? null : "TCP",
                ErrorMessage = success ? null : details
            });
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "Preflight TCP"));
        }

        if (s.CheckTls && targetUri.Scheme == "https")
        {
            var (success, duration, details, days) = await NetworkProbes.TlsProbeAsync(targetUri.Host, 443, ct);
            results.Add(new PreflightResult("TLS")
            {
                Success = success,
                DurationMs = duration,
                Details = days.HasValue ? $"Days to expiry: {days}" : details,
                ErrorType = success ? null : "TLS",
                ErrorMessage = success ? null : details
            });
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "Preflight TLS"));
        }

        if (s.CheckHttp)
        {
            using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(5));
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await client.GetAsync(normalizedTarget, ct);
                sw.Stop();
                results.Add(new PreflightResult("HTTP")
                {
                    Success = response.IsSuccessStatusCode,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    StatusCode = (int)response.StatusCode,
                    ErrorType = response.IsSuccessStatusCode ? null : "HTTP",
                    ErrorMessage = response.IsSuccessStatusCode ? null : response.StatusCode.ToString()
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new PreflightResult("HTTP")
                {
                    Success = false,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ErrorType = ex.GetType().Name,
                    ErrorMessage = ex.Message
                });
            }

            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(totalChecks, 1), "Preflight HTTP"));
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }

    private static string NormalizeTarget(string target)
    {
        var trimmed = (target ?? string.Empty).Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"https://{trimmed}";
    }
}

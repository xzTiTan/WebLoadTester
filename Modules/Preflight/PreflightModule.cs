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

        var results = new List<ResultBase>();
        var targetUri = new Uri(s.Target);

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
        }

        if (s.CheckHttp)
        {
            using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(5));
            try
            {
                var response = await client.GetAsync(s.Target, ct);
                results.Add(new PreflightResult("HTTP")
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
                results.Add(new PreflightResult("HTTP")
                {
                    Success = false,
                    DurationMs = 0,
                    ErrorType = ex.GetType().Name,
                    ErrorMessage = ex.Message
                });
            }
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }
}

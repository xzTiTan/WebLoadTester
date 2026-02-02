using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Network;

namespace WebLoadTester.Modules.NetDiagnostics;

/// <summary>
/// Модуль сетевой диагностики (DNS/TCP/TLS).
/// </summary>
public class NetDiagnosticsModule : ITestModule
{
    public string Id => "net.diagnostics";
    public string DisplayName => "Сетевая диагностика";
    public string Description => "Диагностика DNS/TCP/TLS для локализации сетевых проблем.";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(NetDiagnosticsSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию.
    /// </summary>
    public object CreateDefaultSettings() => new NetDiagnosticsSettings();

    /// <summary>
    /// Проверяет корректность настроек диагностики.
    /// </summary>
    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not NetDiagnosticsSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(s.Hostname))
        {
            errors.Add("Hostname required");
        }

        return errors;
    }

    /// <summary>
    /// Выполняет сетевые проверки и формирует результат.
    /// </summary>
    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (NetDiagnosticsSettings)settings;
        var result = new ModuleResult();

        var results = new List<ResultBase>();

        if (s.EnableDns)
        {
            var (success, duration, details) = await NetworkProbes.DnsProbeAsync(s.Hostname, ct);
            results.Add(new ProbeResult("DNS")
            {
                Success = success,
                DurationMs = duration,
                Details = details,
                ErrorType = success ? null : "DNS",
                ErrorMessage = success ? null : details
            });
        }

        if (s.EnableTcp)
        {
            foreach (var port in s.Ports)
            {
                var (success, duration, details) = await NetworkProbes.TcpProbeAsync(s.Hostname, port, ct);
                results.Add(new ProbeResult($"TCP:{port}")
                {
                    Success = success,
                    DurationMs = duration,
                    Details = details,
                    ErrorType = success ? null : "TCP",
                    ErrorMessage = success ? null : details
                });
            }
        }

        if (s.EnableTls)
        {
            var (success, duration, details, daysToExpiry) = await NetworkProbes.TlsProbeAsync(s.Hostname, 443, ct);
            results.Add(new ProbeResult("TLS")
            {
                Success = success,
                DurationMs = duration,
                Details = daysToExpiry.HasValue ? $"{details} (Days to expiry: {daysToExpiry})" : details,
                ErrorType = success ? null : "TLS",
                ErrorMessage = success ? null : details
            });
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }
}

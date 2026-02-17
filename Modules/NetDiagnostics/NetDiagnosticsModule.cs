using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    public string Description => "Безопасная диагностика DNS/TCP/TLS для локализации сетевых проблем.";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(NetDiagnosticsSettings);

    public object CreateDefaultSettings() => new NetDiagnosticsSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not NetDiagnosticsSettings s)
        {
            errors.Add("Некорректный тип настроек net.diagnostics.");
            return errors;
        }

        s.NormalizeLegacy();

        if (string.IsNullOrWhiteSpace(s.Hostname))
        {
            errors.Add("Hostname обязателен.");
        }

        if (!s.CheckDns && !s.CheckTcp && !s.CheckTls)
        {
            errors.Add("Нужно включить хотя бы одну проверку: DNS, TCP или TLS.");
        }

        if (!s.UseAutoPorts)
        {
            if (s.Ports.Count == 0)
            {
                errors.Add("При ручных портах добавьте минимум один порт.");
            }

            for (var i = 0; i < s.Ports.Count; i++)
            {
                if (s.Ports[i].Port is < 1 or > 65535)
                {
                    errors.Add($"Порт в строке #{i + 1} должен быть в диапазоне 1..65535.");
                }
            }
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (NetDiagnosticsSettings)settings;
        s.NormalizeLegacy();
        var ports = ResolvePorts(s);

        var results = new List<ResultBase>();
        var steps = (s.CheckDns ? 1 : 0) + (s.CheckTcp ? ports.Count : 0) + (s.CheckTls ? ports.Count : 0);
        var current = 0;

        ctx.Progress.Report(new ProgressUpdate(0, Math.Max(steps, 1), "Сетевая диагностика"));

        if (s.CheckDns)
        {
            var (ok, latencyMs, details) = await NetworkProbes.DnsProbeAsync(s.Hostname, ct);
            results.Add(new CheckResult("DNS: resolve")
            {
                Success = ok,
                DurationMs = latencyMs,
                WorkerId = ctx.WorkerId,
                IterationIndex = ctx.Iteration,
                ErrorType = ok ? null : "Network",
                ErrorMessage = ok
                    ? "DNS-разрешение выполнено успешно."
                    : $"DNS-разрешение не удалось: {details}",
                Metrics = JsonSerializer.SerializeToElement(new
                {
                    resolvedIps = ParseResolvedIps(details),
                    latencyMs
                })
            });
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, Math.Max(steps, 1), "DNS"));
        }

        if (s.CheckTcp)
        {
            for (var i = 0; i < ports.Count; i++)
            {
                var port = ports[i];
                var (ok, latencyMs, details) = await NetworkProbes.TcpProbeAsync(s.Hostname, port, ct);
                results.Add(new CheckResult($"TCP: connect :{port}")
                {
                    Success = ok,
                    DurationMs = latencyMs,
                    WorkerId = ctx.WorkerId,
                    IterationIndex = ctx.Iteration,
                    ItemIndex = i,
                    ErrorType = ok ? null : "Network",
                    ErrorMessage = ok
                        ? $"TCP-подключение к порту {port} успешно."
                        : $"TCP-подключение к порту {port} не удалось: {details}",
                    Metrics = JsonSerializer.SerializeToElement(new { port, latencyMs })
                });
                current++;
                ctx.Progress.Report(new ProgressUpdate(current, Math.Max(steps, 1), $"TCP:{port}"));
            }
        }

        if (s.CheckTls)
        {
            for (var i = 0; i < ports.Count; i++)
            {
                var port = ports[i];
                var (ok, latencyMs, details, _) = await NetworkProbes.TlsProbeAsync(s.Hostname, port, ct);
                results.Add(new CheckResult($"TLS: handshake :{port}")
                {
                    Success = ok,
                    DurationMs = latencyMs,
                    WorkerId = ctx.WorkerId,
                    IterationIndex = ctx.Iteration,
                    ItemIndex = i,
                    ErrorType = ok ? null : "Network",
                    ErrorMessage = ok
                        ? $"TLS-рукопожатие на порту {port} успешно."
                        : $"TLS-рукопожатие на порту {port} не удалось: {details}",
                    Metrics = JsonSerializer.SerializeToElement(new
                    {
                        port,
                        protocol = "TLS",
                        cipher = ExtractCipher(details),
                        latencyMs
                    })
                });
                current++;
                ctx.Progress.Report(new ProgressUpdate(current, Math.Max(steps, 1), $"TLS:{port}"));
            }
        }

        return new ModuleResult
        {
            Results = results,
            Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success
        };
    }

    private static List<int> ResolvePorts(NetDiagnosticsSettings settings)
    {
        if (settings.UseAutoPorts)
        {
            return new List<int> { 443 };
        }

        return settings.Ports
            .Select(p => p.Port)
            .Where(p => p is >= 1 and <= 65535)
            .Distinct()
            .ToList();
    }

    private static IReadOnlyList<string> ParseResolvedIps(string details)
    {
        return string.IsNullOrWhiteSpace(details)
            ? Array.Empty<string>()
            : details.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? ExtractCipher(string details)
    {
        return string.IsNullOrWhiteSpace(details) ? null : details;
    }
}

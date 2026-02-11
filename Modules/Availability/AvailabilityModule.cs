using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.Availability;

/// <summary>
/// Модуль проверки доступности HTTP/TCP.
/// </summary>
public class AvailabilityModule : ITestModule
{
    public string Id => "net.availability";
    public string DisplayName => "Доступность";
    public string Description => "Проверяет доступность HTTP/TCP целевого ресурса.";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(AvailabilitySettings);

    public object CreateDefaultSettings() => new AvailabilitySettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not AvailabilitySettings s)
        {
            errors.Add("Некорректный тип настроек.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(s.Target))
        {
            errors.Add("Укажите цель проверки.");
        }

        if (s.TimeoutMs <= 0)
        {
            errors.Add("Таймаут должен быть больше 0 мс.");
        }

        if (s.IntervalSeconds < 0)
        {
            errors.Add("Интервал не может быть отрицательным.");
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (AvailabilitySettings)settings;
        var checks = ResolveChecks(ctx.Profile);
        var interval = TimeSpan.FromSeconds(Math.Max(0, s.IntervalSeconds));

        ctx.Log.Info($"[Availability] Probing {s.TargetType}:{s.Target}; checks={checks}");
        ctx.Progress.Report(new ProgressUpdate(0, checks, "Проверка доступности"));

        var results = new List<ResultBase>();
        for (var i = 0; i < checks; i++)
        {
            ct.ThrowIfCancellationRequested();
            var probe = await ProbeOnceAsync(s, i + 1, ct);
            results.Add(probe);
            ctx.Progress.Report(new ProgressUpdate(i + 1, checks, "Проверка доступности"));

            if (i < checks - 1 && interval > TimeSpan.Zero)
            {
                await Task.Delay(interval, ct);
            }
        }

        var okCount = results.Count(r => r.Success);
        var avgLatency = results.Count == 0 ? 0 : results.Average(r => r.DurationMs);
        ctx.Log.Info($"[Availability] uptime={(double)okCount / Math.Max(1, results.Count):P1}, avg={avgLatency:F0}ms");

        return new ModuleResult
        {
            Results = results,
            Status = okCount == results.Count ? TestStatus.Success : TestStatus.Failed
        };
    }

    private static int ResolveChecks(RunProfile profile)
    {
        var checks = profile.Mode == RunMode.Iterations ? profile.Iterations : Math.Max(1, profile.DurationSeconds);
        return Math.Clamp(checks, 1, 60);
    }

    private static async Task<ProbeResult> ProbeOnceAsync(AvailabilitySettings s, int index, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var success = false;
        string? error = null;

        try
        {
            if (s.TargetType.Equals("Tcp", StringComparison.OrdinalIgnoreCase))
            {
                var parts = s.Target.Split(':', 2);
                if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
                {
                    throw new ArgumentException("Для TCP укажите цель в формате host:port.");
                }

                using var tcp = new TcpClient();
                await tcp.ConnectAsync(parts[0], port, ct);
                success = true;
            }
            else
            {
                using var client = HttpClientProvider.Create(TimeSpan.FromMilliseconds(s.TimeoutMs));
                using var request = new HttpRequestMessage(HttpMethod.Head, s.Target);
                var response = await client.SendAsync(request, ct);
                success = response.IsSuccessStatusCode;
                if (!success)
                {
                    error = response.StatusCode.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            sw.Stop();
        }

        return new ProbeResult($"Проверка {index}")
        {
            Success = success,
            DurationMs = sw.Elapsed.TotalMilliseconds,
            ErrorType = success ? null : "Availability",
            ErrorMessage = error,
            Details = success ? "Available" : "Unavailable"
        };
    }
}

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
            errors.Add("Invalid settings type");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(s.Target))
        {
            errors.Add("Target required");
        }

        if (s.TimeoutMs <= 0)
        {
            errors.Add("TimeoutMs must be positive");
        }

        if (s.IntervalSeconds < 0)
        {
            errors.Add("IntervalSeconds cannot be negative");
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (AvailabilitySettings)settings;
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
                    throw new ArgumentException("TCP target must be host:port");
                }

                using var tcp = new TcpClient();
                await tcp.ConnectAsync(parts[0], port, ct);
                success = true;
            }
            else
            {
                using var client = HttpClientProvider.Create(TimeSpan.FromMilliseconds(s.TimeoutMs));
                var response = await client.GetAsync(s.Target, ct);
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
            success = false;
        }
        finally
        {
            sw.Stop();
        }

        var probe = new ProbeResult("Availability probe")
        {
            Success = success,
            DurationMs = sw.Elapsed.TotalMilliseconds,
            ErrorType = success ? null : "Availability",
            ErrorMessage = error,
            Details = success ? "Available" : "Unavailable"
        };

        if (s.IntervalSeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(s.IntervalSeconds), ct);
        }

        ctx.Progress.Report(new ProgressUpdate(1, 1, DisplayName));

        return new ModuleResult
        {
            Results = new List<ResultBase> { probe },
            Status = success ? TestStatus.Success : TestStatus.Failed
        };
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.Availability;

/// <summary>
/// Модуль периодической проверки доступности HTTP/TCP.
/// </summary>
public class AvailabilityModule : ITestModule
{
    public string Id => "net.availability";
    public string DisplayName => "Доступность";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(AvailabilitySettings);

    /// <summary>
    /// Создаёт настройки по умолчанию.
    /// </summary>
    public object CreateDefaultSettings() => new AvailabilitySettings();

    /// <summary>
    /// Проверяет корректность настроек доступности.
    /// </summary>
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

        if (s.IntervalSeconds < 5)
        {
            errors.Add("IntervalSeconds too low");
        }

        return errors;
    }

    /// <summary>
    /// Запускает цикл проверок доступности и формирует отчёт.
    /// </summary>
    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (AvailabilitySettings)settings;
        var report = new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Completed,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            AppVersion = typeof(AvailabilityModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };

        var results = new List<ResultBase>();
        var totalChecks = (int)(TimeSpan.FromMinutes(s.DurationMinutes).TotalSeconds / s.IntervalSeconds);
        var client = HttpClientProvider.Create(TimeSpan.FromMilliseconds(s.TimeoutMs));
        var consecutiveFails = 0;

        for (var i = 0; i < totalChecks; i++)
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            var success = false;
            string? error = null;

            try
            {
                if (s.TargetType.Equals("Tcp", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = s.Target.Split(':');
                    using var tcp = new TcpClient();
                    await tcp.ConnectAsync(parts[0], int.Parse(parts[1]), ct);
                    success = true;
                }
                else
                {
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

            sw.Stop();
            if (!success)
            {
                consecutiveFails++;
            }
            else
            {
                consecutiveFails = 0;
            }

            results.Add(new ProbeResult($"Check {i + 1}")
            {
                Success = success,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                ErrorType = success ? null : "Availability",
                ErrorMessage = error,
                Details = consecutiveFails >= s.FailThreshold ? "Downtime window" : ""
            });

            ctx.Progress.Report(new ProgressUpdate(i + 1, totalChecks, "Availability"));
            await Task.Delay(TimeSpan.FromSeconds(s.IntervalSeconds), ct);
        }

        report.Results = results;
        report.FinishedAt = ctx.Now;
        return report;
    }
}

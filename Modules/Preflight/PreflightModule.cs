using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;
using WebLoadTester.Infrastructure.Network;
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.Preflight;

/// <summary>
/// Модуль предварительных системных и сетевых проверок.
/// </summary>
public class PreflightModule : ITestModule
{
    public string Id => "net.preflight";
    public string DisplayName => "Предварительные проверки";
    public string Description => "Быстро проверяет готовность окружения и цели перед запуском.";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(PreflightSettings);

    public object CreateDefaultSettings() => new PreflightSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not PreflightSettings)
        {
            errors.Add("Некорректный тип настроек.");
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (PreflightSettings)settings;
        var target = NormalizeTarget(s.Target);

        var checks = 3 + (s.CheckDns ? 1 : 0) + (s.CheckTcp ? 1 : 0) + (s.CheckTls ? 1 : 0) + (s.CheckHttp ? 1 : 0);
        var current = 0;
        var results = new List<ResultBase>();

        void ReportProgress(string stage)
        {
            current++;
            ctx.Progress.Report(new ProgressUpdate(current, checks, stage));
        }

        ctx.Progress.Report(new ProgressUpdate(0, checks, "Preflight старт"));

        // 1) Доступность каталогов данных/runs.
        var fsCheck = CheckWritableDirectory(ctx.Artifacts.RunsRoot);
        results.Add(new PreflightResult("Filesystem")
        {
            Success = fsCheck.success,
            DurationMs = fsCheck.durationMs,
            Details = fsCheck.message,
            ErrorType = fsCheck.success ? null : "Warn",
            ErrorMessage = fsCheck.success ? null : fsCheck.message
        });
        ReportProgress("Preflight FS");

        // 2) SQLite connectivity (минимальная проверка открытия).
        var sqliteCheck = await CheckSqliteAsync(ctx.Artifacts.RunsRoot, ct);
        results.Add(new PreflightResult("SQLite")
        {
            Success = sqliteCheck.success,
            DurationMs = sqliteCheck.durationMs,
            Details = sqliteCheck.message,
            ErrorType = sqliteCheck.success ? null : "Warn",
            ErrorMessage = sqliteCheck.success ? null : sqliteCheck.message
        });
        ReportProgress("Preflight SQLite");

        // 3) Доступность Chromium (warning, не фатально).
        var chromiumAvailable = PlaywrightFactory.HasBrowsersInstalled();
        var chromiumMessage = chromiumAvailable
            ? "Chromium найден."
            : $"Chromium не найден. Установите браузер в {PlaywrightFactory.GetBrowsersPath()}";
        results.Add(new PreflightResult("Chromium")
        {
            Success = true,
            DurationMs = 0,
            Details = chromiumMessage,
            ErrorType = chromiumAvailable ? null : "Warn",
            ErrorMessage = chromiumAvailable ? null : chromiumMessage
        });
        if (!chromiumAvailable)
        {
            ctx.Log.Warn($"[Preflight] {chromiumMessage}");
        }

        ReportProgress("Preflight Chromium");

        if (!string.IsNullOrWhiteSpace(target))
        {
            var targetUri = new Uri(target);
            if (s.CheckDns)
            {
                var (success, duration, details) = await NetworkProbes.DnsProbeAsync(targetUri.Host, ct);
                results.Add(ToResult("DNS", success, duration, details));
                ReportProgress("Preflight DNS");
            }

            if (s.CheckTcp)
            {
                var port = targetUri.Port == -1 ? (targetUri.Scheme == "https" ? 443 : 80) : targetUri.Port;
                var (success, duration, details) = await NetworkProbes.TcpProbeAsync(targetUri.Host, port, ct);
                results.Add(ToResult("TCP", success, duration, details));
                ReportProgress("Preflight TCP");
            }

            if (s.CheckTls && targetUri.Scheme == "https")
            {
                var (success, duration, details, days) = await NetworkProbes.TlsProbeAsync(targetUri.Host, 443, ct);
                var tlsDetails = days.HasValue ? $"{details}; дней до истечения: {days}" : details;
                results.Add(ToResult("TLS", success, duration, tlsDetails));
                ReportProgress("Preflight TLS");
            }

            if (s.CheckHttp)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(5));
                    var response = await client.GetAsync(target, ct);
                    sw.Stop();
                    results.Add(ToResult("HTTP", response.IsSuccessStatusCode, sw.Elapsed.TotalMilliseconds, response.StatusCode.ToString()));
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results.Add(ToResult("HTTP", false, sw.Elapsed.TotalMilliseconds, ex.Message));
                }

                ReportProgress("Preflight HTTP");
            }
        }

        return new ModuleResult
        {
            Results = results,
            Status = TestStatus.Success
        };
    }

    private static PreflightResult ToResult(string name, bool success, double durationMs, string details)
    {
        return new PreflightResult(name)
        {
            Success = success,
            DurationMs = durationMs,
            Details = details,
            ErrorType = success ? null : "Warn",
            ErrorMessage = success ? null : details
        };
    }

    private static string NormalizeTarget(string target)
    {
        var trimmed = (target ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"https://{trimmed}";
    }

    private static (bool success, double durationMs, string message) CheckWritableDirectory(string path)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            Directory.CreateDirectory(path);
            var probeFile = Path.Combine(path, $".preflight_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, "ok");
            File.Delete(probeFile);
            sw.Stop();
            return (true, sw.Elapsed.TotalMilliseconds, $"Каталог доступен: {path}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }

    private static async Task<(bool success, double durationMs, string message)> CheckSqliteAsync(string runsRoot, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var dbPath = Path.Combine(runsRoot, "..", "webloadtester.db");
            await using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(ct);
            sw.Stop();
            return (true, sw.Elapsed.TotalMilliseconds, "SQLite доступна.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return (false, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }
}

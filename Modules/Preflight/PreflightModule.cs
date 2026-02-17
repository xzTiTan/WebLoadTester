using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
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
/// Модуль предварительных проверок окружения и цели.
/// </summary>
public class PreflightModule : ITestModule
{
    public string Id => "net.preflight";
    public string DisplayName => "Preflight";
    public string Description => "Проверяет окружение перед основным запуском.";
    public TestFamily Family => TestFamily.NetSec;
    public Type SettingsType => typeof(PreflightSettings);

    public object CreateDefaultSettings() => new PreflightSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not PreflightSettings s)
        {
            errors.Add("Некорректный тип настроек net.preflight.");
            return errors;
        }

        if (!s.CheckDns && !s.CheckTcp && !s.CheckTls && !s.CheckHttp)
        {
            errors.Add("Включите хотя бы одну проверку preflight (DNS/TCP/TLS/HTTP).");
        }

        var target = NormalizeTarget(s.Target);
        if (!string.IsNullOrWhiteSpace(target) && !Uri.TryCreate(target, UriKind.Absolute, out _))
        {
            errors.Add("Target должен быть корректным URL или hostname.");
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (PreflightSettings)settings;
        var target = NormalizeTarget(s.Target);
        var results = new List<ResultBase>();

        var checksCount = 3 + (string.IsNullOrWhiteSpace(target) ? 0 : (s.CheckDns ? 1 : 0) + (s.CheckTcp ? 1 : 0) + (s.CheckTls ? 1 : 0) + (s.CheckHttp ? 1 : 0));
        var completed = 0;

        void ReportProgress(string stage)
        {
            completed++;
            ctx.Progress.Report(new ProgressUpdate(completed, Math.Max(checksCount, 1), stage));
        }

        var (runsWritable, runsDuration, runsMessage) = CheckWritableDirectory(ctx.Artifacts.RunsRoot);
        results.Add(ToResult("Preflight runs directory", runsWritable, runsDuration, runsMessage, "Environment", JsonSerializer.SerializeToElement(new { path = ctx.Artifacts.RunsRoot }), ctx.WorkerId, ctx.Iteration));
        ReportProgress("Preflight runs directory");

        var (sqliteOk, sqliteDuration, sqliteMessage) = await CheckSqliteAsync(ctx.Artifacts.RunsRoot, ct);
        results.Add(ToResult("Preflight SQLite", sqliteOk, sqliteDuration, sqliteMessage, sqliteOk ? null : "Environment", JsonSerializer.SerializeToElement(new { database = "webloadtester.db" }), ctx.WorkerId, ctx.Iteration));
        ReportProgress("Preflight SQLite");

        var chromiumAvailable = PlaywrightFactory.HasBrowsersInstalled();
        var chromiumMessage = chromiumAvailable
            ? "Chromium установлен."
            : $"Chromium не найден. Установите браузер в {PlaywrightFactory.GetBrowsersPath()}";
        results.Add(ToResult("Preflight Chromium", true, 0, chromiumMessage, chromiumAvailable ? null : "Warn", JsonSerializer.SerializeToElement(new { browsersPath = PlaywrightFactory.GetBrowsersPath(), installed = chromiumAvailable }), ctx.WorkerId, ctx.Iteration));
        ReportProgress("Preflight Chromium");

        if (!string.IsNullOrWhiteSpace(target))
        {
            var targetUri = new Uri(target);
            if (s.CheckDns)
            {
                var (success, duration, details) = await NetworkProbes.DnsProbeAsync(targetUri.Host, ct);
                results.Add(ToResult("Preflight DNS", success, duration, success ? "DNS-проверка успешна." : details, success ? null : "Network", JsonSerializer.SerializeToElement(new { host = targetUri.Host, details }), ctx.WorkerId, ctx.Iteration));
                ReportProgress("Preflight DNS");
            }

            if (s.CheckTcp)
            {
                var port = targetUri.Port == -1 ? (targetUri.Scheme == "https" ? 443 : 80) : targetUri.Port;
                var (success, duration, details) = await NetworkProbes.TcpProbeAsync(targetUri.Host, port, ct);
                results.Add(ToResult($"Preflight TCP :{port}", success, duration, success ? "TCP-подключение успешно." : details, success ? null : "Network", JsonSerializer.SerializeToElement(new { host = targetUri.Host, port, latencyMs = duration }), ctx.WorkerId, ctx.Iteration));
                ReportProgress("Preflight TCP");
            }

            if (s.CheckTls && targetUri.Scheme == "https")
            {
                var port = targetUri.Port == -1 ? 443 : targetUri.Port;
                var (success, duration, details, _) = await NetworkProbes.TlsProbeAsync(targetUri.Host, port, ct);
                results.Add(ToResult($"Preflight TLS :{port}", success, duration, success ? "TLS-рукопожатие успешно." : details, success ? null : "Network", JsonSerializer.SerializeToElement(new { host = targetUri.Host, port, details, latencyMs = duration }), ctx.WorkerId, ctx.Iteration));
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
                    results.Add(ToResult("Preflight HTTP", response.IsSuccessStatusCode, sw.Elapsed.TotalMilliseconds,
                        response.IsSuccessStatusCode ? "HTTP-проверка успешна." : $"HTTP {(int)response.StatusCode}",
                        response.IsSuccessStatusCode ? null : "Http",
                        JsonSerializer.SerializeToElement(new { endpoint = target, statusCode = (int)response.StatusCode, latencyMs = sw.Elapsed.TotalMilliseconds }), ctx.WorkerId, ctx.Iteration));
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results.Add(ToResult("Preflight HTTP", false, sw.Elapsed.TotalMilliseconds, ex.Message, "Network", JsonSerializer.SerializeToElement(new { endpoint = target, latencyMs = sw.Elapsed.TotalMilliseconds }), ctx.WorkerId, ctx.Iteration));
                }

                ReportProgress("Preflight HTTP");
            }
        }

        return new ModuleResult
        {
            Results = results,
            Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success
        };
    }

    private static PreflightResult ToResult(string name, bool success, double durationMs, string details, string? errorKind, JsonElement metrics, int workerId, int iteration)
    {
        return new PreflightResult(name)
        {
            Success = success,
            DurationMs = durationMs,
            Details = details,
            ErrorType = success ? null : errorKind,
            ErrorMessage = details,
            WorkerId = workerId,
            IterationIndex = iteration,
            Metrics = metrics
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

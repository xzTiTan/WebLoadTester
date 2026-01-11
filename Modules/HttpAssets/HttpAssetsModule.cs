using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpAssets;

/// <summary>
/// Модуль проверки ассетов по HTTP (тип, размер, латентность).
/// </summary>
public class HttpAssetsModule : ITestModule
{
    public string Id => "http.assets";
    public string DisplayName => "HTTP ассеты";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpAssetsSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию с примером ассета.
    /// </summary>
    public object CreateDefaultSettings()
    {
        return new HttpAssetsSettings
        {
            Assets = new List<AssetItem>
            {
                new() { Url = "https://example.com" }
            }
        };
    }

    /// <summary>
    /// Проверяет корректность списка ассетов.
    /// </summary>
    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not HttpAssetsSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (s.Assets.Count == 0)
        {
            errors.Add("At least one asset required");
        }

        return errors;
    }

    /// <summary>
    /// Выполняет проверки ассетов и формирует отчёт.
    /// </summary>
    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpAssetsSettings)settings;
        var report = new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Completed,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            AppVersion = typeof(HttpAssetsModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };

        using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(s.TimeoutSeconds));
        var results = new List<ResultBase>();
        var current = 0;

        foreach (var asset in s.Assets)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var response = await client.GetAsync(asset.Url, ct);
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                sw.Stop();
                var success = response.IsSuccessStatusCode;
                var error = string.Empty;

                if (asset.ExpectedContentType != null &&
                    (!response.Content.Headers.ContentType?.MediaType?.Contains(asset.ExpectedContentType) ?? true))
                {
                    success = false;
                    error = "Content-Type mismatch";
                }

                if (asset.MaxSizeBytes.HasValue && bytes.Length > asset.MaxSizeBytes)
                {
                    success = false;
                    error = "Asset size exceeded";
                }

                if (asset.MaxLatencyMs.HasValue && sw.Elapsed.TotalMilliseconds > asset.MaxLatencyMs)
                {
                    success = false;
                    error = "Latency exceeded";
                }

                results.Add(new CheckResult(asset.Url)
                {
                    Success = success,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    StatusCode = (int)response.StatusCode,
                    ErrorType = success ? null : "Asset",
                    ErrorMessage = error
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new CheckResult(asset.Url)
                {
                    Success = false,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ErrorType = ex.GetType().Name,
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                current++;
                ctx.Progress.Report(new ProgressUpdate(current, s.Assets.Count, "HTTP Assets"));
            }
        }

        report.Results = results;
        report.FinishedAt = ctx.Now;
        return report;
    }
}

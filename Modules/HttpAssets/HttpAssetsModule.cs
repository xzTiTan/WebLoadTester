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

namespace WebLoadTester.Modules.HttpAssets;

/// <summary>
/// Модуль проверки ассетов по HTTP (тип, размер, латентность).
/// </summary>
public class HttpAssetsModule : ITestModule
{
    public string Id => "http.assets";
    public string DisplayName => "HTTP ассеты";
    public string Description => "Проверяет статические ресурсы на доступность, тип, размер и задержку.";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpAssetsSettings);

    public object CreateDefaultSettings()
    {
        return new HttpAssetsSettings
        {
            Assets = new List<AssetItem>
            {
                new() { Url = "https://www.google.com/favicon.ico", Name = "Google favicon" }
            }
        };
    }

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not HttpAssetsSettings s)
        {
            errors.Add("Неверный тип настроек HTTP assets.");
            return errors;
        }

        if (s.TimeoutSeconds <= 0)
        {
            errors.Add("TimeoutSeconds должен быть больше 0.");
        }

        if (s.Assets.Count == 0)
        {
            errors.Add("Список Assets должен содержать хотя бы один элемент.");
            return errors;
        }

        for (var i = 0; i < s.Assets.Count; i++)
        {
            var asset = s.Assets[i];
            asset.NormalizeLegacy();
            var prefix = $"Asset #{i + 1}";

            if (!Uri.TryCreate(asset.Url, UriKind.Absolute, out _))
            {
                errors.Add($"{prefix}: Url обязателен и должен быть абсолютным URL.");
            }

            if (asset.MaxLatencyMs.HasValue && asset.MaxLatencyMs <= 0)
            {
                errors.Add($"{prefix}: MaxLatencyMs должен быть больше 0.");
            }

            if (asset.MaxSizeKb.HasValue && asset.MaxSizeKb <= 0)
            {
                errors.Add($"{prefix}: MaxSizeKb должен быть больше 0.");
            }
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpAssetsSettings)settings;
        foreach (var asset in s.Assets)
        {
            asset.NormalizeLegacy();
        }

        var result = new ModuleResult();
        ctx.Log.Info($"[HttpAssets] Assets={s.Assets.Count}");

        using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(s.TimeoutSeconds));
        var results = new List<ResultBase>();

        for (var i = 0; i < s.Assets.Count; i++)
        {
            var asset = s.Assets[i];
            var sw = Stopwatch.StartNew();

            var success = true;
            string message = "Проверка ассета прошла успешно.";
            string? errorKind = null;
            int? statusCode = null;
            long bytes = 0;
            string? contentType = null;

            try
            {
                var response = await client.GetAsync(asset.Url, ct);
                var payload = await response.Content.ReadAsByteArrayAsync(ct);
                sw.Stop();

                statusCode = (int)response.StatusCode;
                bytes = payload.LongLength;
                contentType = response.Content.Headers.ContentType?.MediaType;

                if (!response.IsSuccessStatusCode)
                {
                    success = false;
                    message = $"Ассет вернул HTTP {statusCode}.";
                    errorKind = "Http";
                }
                else if (!string.IsNullOrWhiteSpace(asset.ExpectedContentType) &&
                         (contentType == null || !contentType.Contains(asset.ExpectedContentType, StringComparison.OrdinalIgnoreCase)))
                {
                    success = false;
                    message = $"Ожидали Content-Type '{asset.ExpectedContentType}', получили '{contentType ?? "(empty)"}'.";
                    errorKind = "AssertFailed";
                }
                else if (asset.MaxSizeKb.HasValue && bytes > asset.MaxSizeKb.Value * 1024L)
                {
                    success = false;
                    message = $"Размер {bytes} байт превышает лимит {asset.MaxSizeKb.Value} КБ.";
                    errorKind = "AssertFailed";
                }
                else if (asset.MaxLatencyMs.HasValue && sw.Elapsed.TotalMilliseconds > asset.MaxLatencyMs.Value)
                {
                    success = false;
                    message = $"Задержка {sw.Elapsed.TotalMilliseconds:F0} мс превышает лимит {asset.MaxLatencyMs.Value} мс.";
                    errorKind = "AssertFailed";
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException ex)
            {
                sw.Stop();
                success = false;
                message = ex.Message;
                errorKind = "Timeout";
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                success = false;
                message = ex.Message;
                errorKind = "Network";
            }
            catch (Exception ex)
            {
                sw.Stop();
                success = false;
                message = ex.Message;
                errorKind = "Exception";
            }

            results.Add(new AssetResult(asset.Name ?? asset.Url)
            {
                Success = success,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                WorkerId = ctx.WorkerId,
                IterationIndex = ctx.Iteration,
                ItemIndex = i,
                ErrorType = errorKind,
                ErrorMessage = success ? "ok" : message,
                StatusCode = statusCode,
                LatencyMs = sw.Elapsed.TotalMilliseconds,
                Bytes = bytes,
                ContentType = contentType
            });

            ctx.Progress.Report(new ProgressUpdate(i + 1, s.Assets.Count, "HTTP ассеты"));
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }
}

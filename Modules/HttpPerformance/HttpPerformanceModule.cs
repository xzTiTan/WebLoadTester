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

namespace WebLoadTester.Modules.HttpPerformance;

/// <summary>
/// Модуль HTTP-проверок производительности.
/// </summary>
public class HttpPerformanceModule : ITestModule
{
    public string Id => "http.performance";
    public string DisplayName => "HTTP производительность";
    public string Description => "Оценивает задержки и доступность endpoint-ов.";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpPerformanceSettings);

    public object CreateDefaultSettings()
    {
        return new HttpPerformanceSettings
        {
            Endpoints = new List<HttpPerformanceEndpoint>
            {
                new() { Name = "Endpoint 1", Method = "GET", Path = "/", ExpectedStatusCode = 200 }
            }
        };
    }

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not HttpPerformanceSettings s)
        {
            errors.Add("Неверный тип настроек HTTP performance.");
            return errors;
        }

        if (!Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("BaseUrl обязателен и должен быть абсолютным URL.");
        }

        if (s.TimeoutSeconds <= 0)
        {
            errors.Add("TimeoutSeconds должен быть больше 0.");
        }

        if (s.Endpoints.Count == 0)
        {
            errors.Add("Список Endpoints должен содержать хотя бы один элемент.");
            return errors;
        }

        var duplicates = s.Endpoints
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .GroupBy(e => e.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            errors.Add($"Endpoint.Name должен быть уникальным: {duplicate}.");
        }

        for (var i = 0; i < s.Endpoints.Count; i++)
        {
            var endpoint = s.Endpoints[i];
            var prefix = $"Endpoint #{i + 1}";

            if (string.IsNullOrWhiteSpace(endpoint.Name))
            {
                errors.Add($"{prefix}: Name обязателен.");
            }

            if (string.IsNullOrWhiteSpace(endpoint.Method))
            {
                errors.Add($"{prefix}: Method обязателен.");
            }

            if (string.IsNullOrWhiteSpace(endpoint.Path))
            {
                errors.Add($"{prefix}: Path обязателен.");
            }

            if (endpoint.ExpectedStatusCode.HasValue && endpoint.ExpectedStatusCode is < 100 or > 599)
            {
                errors.Add($"{prefix}: ExpectedStatusCode должен быть в диапазоне 100..599.");
            }
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpPerformanceSettings)settings;
        var result = new ModuleResult();
        ctx.Log.Info($"[HttpPerformance] BaseUrl={s.BaseUrl}, endpoints={s.Endpoints.Count}");

        using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(s.TimeoutSeconds));
        var results = new List<ResultBase>();

        for (var i = 0; i < s.Endpoints.Count; i++)
        {
            var endpoint = s.Endpoints[i];
            var sw = Stopwatch.StartNew();

            var success = true;
            string message = "Проверка прошла успешно.";
            string? errorKind = null;
            int? statusCode = null;

            try
            {
                using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), ResolveUrl(s.BaseUrl, endpoint.Path));
                var response = await client.SendAsync(request, ct);
                sw.Stop();

                statusCode = (int)response.StatusCode;
                if (endpoint.ExpectedStatusCode.HasValue && statusCode != endpoint.ExpectedStatusCode.Value)
                {
                    success = false;
                    message = $"Ожидали код {endpoint.ExpectedStatusCode}, получили {statusCode}.";
                    errorKind = "AssertFailed";
                }
                else if (!response.IsSuccessStatusCode)
                {
                    success = false;
                    message = $"Endpoint вернул HTTP {statusCode}.";
                    errorKind = "Http";
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

            results.Add(new EndpointResult(endpoint.Name)
            {
                Success = success,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                WorkerId = ctx.WorkerId,
                IterationIndex = ctx.Iteration,
                ItemIndex = i,
                ErrorType = errorKind,
                ErrorMessage = success ? "ok" : message,
                StatusCode = statusCode,
                LatencyMs = sw.Elapsed.TotalMilliseconds
            });

            ctx.Progress.Report(new ProgressUpdate(i + 1, s.Endpoints.Count, "HTTP производительность"));
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }

    private static string ResolveUrl(string baseUrl, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        var root = new Uri(baseUrl, UriKind.Absolute);
        return new Uri(root, path).ToString();
    }
}

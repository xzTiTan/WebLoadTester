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
/// Модуль нагрузочного теста HTTP с конкуренцией и ограничением RPS.
/// </summary>
public class HttpPerformanceModule : ITestModule
{
    public string Id => "http.performance";
    public string DisplayName => "HTTP производительность";
    public string Description => "Оценивает задержки и устойчивость HTTP при контролируемой параллельности.";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpPerformanceSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию.
    /// </summary>
    public object CreateDefaultSettings()
    {
        return new HttpPerformanceSettings
        {
            Endpoints = new List<HttpPerformanceEndpoint>
            {
                new() { Name = "Example", Method = "GET", Path = "/" }
            }
        };
    }

    /// <summary>
    /// Проверяет корректность параметров нагрузки.
    /// </summary>
    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not HttpPerformanceSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (!Uri.TryCreate(s.BaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("BaseUrl is required");
        }

        if (s.Endpoints.Count == 0)
        {
            errors.Add("At least one endpoint required");
        }

        foreach (var endpoint in s.Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Path))
            {
                errors.Add("Endpoint path is required");
            }

            if (string.IsNullOrWhiteSpace(endpoint.Method))
            {
                errors.Add("Endpoint method is required");
            }
        }

        if (s.TimeoutSeconds <= 0)
        {
            errors.Add("TimeoutSeconds must be positive");
        }

        return errors;
    }

    /// <summary>
    /// Выполняет серию HTTP-запросов и формирует результат.
    /// </summary>
    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpPerformanceSettings)settings;
        var result = new ModuleResult();
        ctx.Log.Info($"[HttpPerformance] BaseUrl={s.BaseUrl}, endpoints={s.Endpoints.Count}, parallelism={ctx.Profile.Parallelism}");

        using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(s.TimeoutSeconds));
        var results = new List<ResultBase>();
        var completed = 0;
        var total = s.Endpoints.Count;

        foreach (var endpoint in s.Endpoints)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), ResolveUrl(s.BaseUrl, endpoint.Path));
                var response = await client.SendAsync(request, ct);
                sw.Stop();

                var success = response.IsSuccessStatusCode;
                string? error = null;

                if (endpoint.ExpectedStatusCode.HasValue && (int)response.StatusCode != endpoint.ExpectedStatusCode)
                {
                    success = false;
                    error = $"Status {(int)response.StatusCode} expected {endpoint.ExpectedStatusCode}";
                }

                results.Add(new CheckResult(endpoint.Name)
                {
                    Success = success,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    StatusCode = (int)response.StatusCode,
                    ErrorType = success ? null : "Http",
                    ErrorMessage = error
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new CheckResult(endpoint.Name)
                {
                    Success = false,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ErrorType = ex.GetType().Name,
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                completed++;
                ctx.Progress.Report(new ProgressUpdate(completed, total, "HTTP производительность"));
            }
        }

        result.Results = results;
        result.Status = result.Results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
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

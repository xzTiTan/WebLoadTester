using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpFunctional;

/// <summary>
/// Модуль функциональных проверок HTTP-эндпоинтов.
/// </summary>
public class HttpFunctionalModule : ITestModule
{
    public string Id => "http.functional";
    public string DisplayName => "HTTP функциональные проверки";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpFunctionalSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию с примером эндпоинта.
    /// </summary>
    public object CreateDefaultSettings()
    {
        return new HttpFunctionalSettings
        {
            Endpoints = new List<HttpEndpoint>
            {
                new() { Name = "Example", Url = "https://example.com" }
            }
        };
    }

    /// <summary>
    /// Проверяет корректность списка эндпоинтов.
    /// </summary>
    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not HttpFunctionalSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (s.Endpoints.Count == 0)
        {
            errors.Add("At least one endpoint required");
        }

        foreach (var endpoint in s.Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Url))
            {
                errors.Add("Endpoint URL is required");
            }
        }

        return errors;
    }

    /// <summary>
    /// Выполняет функциональные проверки и формирует отчёт.
    /// </summary>
    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpFunctionalSettings)settings;
        var report = new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Completed,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            AppVersion = typeof(HttpFunctionalModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };

        using var client = HttpClientProvider.Create(TimeSpan.FromSeconds(s.TimeoutSeconds));
        var results = new List<ResultBase>();
        var current = 0;

        foreach (var endpoint in s.Endpoints)
        {
            var sw = Stopwatch.StartNew();
            var success = true;
            string? error = null;
            try
            {
                var request = new HttpRequestMessage(endpoint.Method, endpoint.Url);
                foreach (var header in endpoint.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                if (!string.IsNullOrWhiteSpace(endpoint.Body))
                {
                    request.Content = new StringContent(endpoint.Body);
                }

                var response = await client.SendAsync(request, ct);
                sw.Stop();
                var body = await response.Content.ReadAsStringAsync(ct);

                if (endpoint.StatusCodeEquals.HasValue && (int)response.StatusCode != endpoint.StatusCodeEquals)
                {
                    success = false;
                    error = $"Status {(int)response.StatusCode} expected {endpoint.StatusCodeEquals}";
                }

                if (endpoint.MaxLatencyMs.HasValue && sw.Elapsed.TotalMilliseconds > endpoint.MaxLatencyMs)
                {
                    success = false;
                    error = $"Latency {sw.Elapsed.TotalMilliseconds:F0}ms > {endpoint.MaxLatencyMs}";
                }

                if (!string.IsNullOrWhiteSpace(endpoint.HeaderContainsKey))
                {
                    if (!response.Headers.TryGetValues(endpoint.HeaderContainsKey, out var values) ||
                        (endpoint.HeaderContainsValue != null && !string.Join(";", values).Contains(endpoint.HeaderContainsValue)))
                    {
                        success = false;
                        error = $"Header {endpoint.HeaderContainsKey} missing";
                    }
                }

                if (!string.IsNullOrWhiteSpace(endpoint.BodyContains) && !body.Contains(endpoint.BodyContains))
                {
                    success = false;
                    error = "Body assertion failed";
                }

                results.Add(new CheckResult(endpoint.Name)
                {
                    Success = success,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    StatusCode = (int)response.StatusCode,
                    ErrorType = success ? null : "Assert",
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
                current++;
                ctx.Progress.Report(new ProgressUpdate(current, s.Endpoints.Count, "HTTP Functional"));
            }
        }

        report.Results = results;
        report.FinishedAt = ctx.Now;
        return report;
    }
}

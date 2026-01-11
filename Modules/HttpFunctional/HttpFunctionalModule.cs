using System.Diagnostics;
using System.Text.Json;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpFunctional;

public sealed class HttpFunctionalModule : ITestModule
{
    private readonly HttpClientProvider _clientProvider = new();

    public string Id => "http.functional";
    public string DisplayName => "HTTP Functional";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpFunctionalSettings);

    public object CreateDefaultSettings() => new HttpFunctionalSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        var s = (HttpFunctionalSettings)settings;
        var errors = new List<string>();
        if (s.Endpoints.Count == 0)
        {
            errors.Add("At least one endpoint is required.");
        }

        return errors;
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext context, CancellationToken ct)
    {
        var s = (HttpFunctionalSettings)settings;
        var report = CreateReportTemplate(context, s);
        var results = new List<CheckResult>();
        using var client = _clientProvider.Create(TimeSpan.FromSeconds(s.TimeoutSeconds));

        foreach (var endpoint in s.Endpoints)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), endpoint.Url);
                foreach (var header in endpoint.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                if (!string.IsNullOrWhiteSpace(endpoint.Body))
                {
                    request.Content = new StringContent(endpoint.Body);
                }

                using var response = await client.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                var checks = ValidateResponse(endpoint, response, body, sw.ElapsedMilliseconds);
                results.Add(new CheckResult
                {
                    Kind = "http-check",
                    Name = endpoint.Name,
                    Success = checks.Success,
                    DurationMs = sw.ElapsedMilliseconds,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = checks.Error
                });
            }
            catch (Exception ex)
            {
                results.Add(new CheckResult
                {
                    Kind = "http-check",
                    Name = endpoint.Name,
                    Success = false,
                    DurationMs = sw.ElapsedMilliseconds,
                    ErrorMessage = ex.Message,
                    ErrorType = ex.GetType().Name
                });
            }

            context.Progress.Report(results.Count, s.Endpoints.Count);
        }

        report.Results.AddRange(results);
        report = report with { Metrics = Core.Services.Metrics.MetricsCalculator.Calculate(report.Results) };
        report = report with { Meta = report.Meta with { Status = TestStatus.Completed, FinishedAt = context.Now } };
        return report;
    }

    private static (bool Success, string? Error) ValidateResponse(HttpEndpoint endpoint, HttpResponseMessage response, string body, long latencyMs)
    {
        var asserts = endpoint.Asserts;
        if (asserts.StatusCodeEquals.HasValue && (int)response.StatusCode != asserts.StatusCodeEquals.Value)
        {
            return (false, $"Status code mismatch: {(int)response.StatusCode}");
        }

        if (asserts.MaxLatencyMs.HasValue && latencyMs > asserts.MaxLatencyMs.Value)
        {
            return (false, $"Latency exceeded: {latencyMs}ms");
        }

        foreach (var header in asserts.HeaderContains)
        {
            if (!response.Headers.TryGetValues(header.Key, out var values) || !values.Any(v => v.Contains(header.Value, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, $"Header {header.Key} missing {header.Value}");
            }
        }

        if (!string.IsNullOrWhiteSpace(asserts.BodyContains) && !body.Contains(asserts.BodyContains, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Body substring not found");
        }

        if (!string.IsNullOrWhiteSpace(asserts.JsonKeyExists))
        {
            using var doc = JsonDocument.Parse(body);
            if (!TryGetJsonValue(doc.RootElement, asserts.JsonKeyExists, out _))
            {
                return (false, $"Json path {asserts.JsonKeyExists} not found");
            }
        }

        if (!string.IsNullOrWhiteSpace(asserts.JsonValueEqualsPath))
        {
            using var doc = JsonDocument.Parse(body);
            if (!TryGetJsonValue(doc.RootElement, asserts.JsonValueEqualsPath, out var element))
            {
                return (false, $"Json path {asserts.JsonValueEqualsPath} not found");
            }

            if (asserts.JsonValueEqualsExpected is not null && element.ToString() != asserts.JsonValueEqualsExpected)
            {
                return (false, $"Json value mismatch: {element}");
            }
        }

        return (true, null);
    }

    private static bool TryGetJsonValue(JsonElement element, string path, out JsonElement value)
    {
        var current = element;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains('['))
            {
                var name = part[..part.IndexOf('[', StringComparison.Ordinal)];
                var indexPart = part[(part.IndexOf('[', StringComparison.Ordinal) + 1)..part.IndexOf(']')];
                if (!current.TryGetProperty(name, out var arrayElement))
                {
                    value = default;
                    return false;
                }

                if (!int.TryParse(indexPart, out var idx) || arrayElement.ValueKind != JsonValueKind.Array || arrayElement.GetArrayLength() <= idx)
                {
                    value = default;
                    return false;
                }

                current = arrayElement[idx];
            }
            else
            {
                if (!current.TryGetProperty(part, out var next))
                {
                    value = default;
                    return false;
                }
                current = next;
            }
        }

        value = current;
        return true;
    }

    private static TestReport CreateReportTemplate(IRunContext context, HttpFunctionalSettings settings)
    {
        return new TestReport
        {
            Meta = new ReportMeta
            {
                Status = TestStatus.Completed,
                StartedAt = context.Now,
                FinishedAt = context.Now,
                AppVersion = typeof(HttpFunctionalModule).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem = Environment.OSVersion.ToString()
            },
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings)
        };
    }
}

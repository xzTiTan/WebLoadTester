using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpFunctional;

public sealed class HttpFunctionalModule : ITestModule
{
    private readonly HttpClientProvider _provider = new();

    public string Id => "http-functional";
    public string DisplayName => "HTTP Functional";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpFunctionalSettings);

    public object CreateDefaultSettings() => new HttpFunctionalSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        if (settings is not HttpFunctionalSettings s)
        {
            return new[] { "Invalid settings" };
        }

        if (s.Endpoints.Count == 0)
        {
            return new[] { "Endpoints list is empty" };
        }

        return Array.Empty<string>();
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpFunctionalSettings)settings;
        var start = ctx.Now;
        var results = new List<ResultItem>();

        foreach (var endpoint in s.Endpoints)
        {
            var sw = Stopwatch.StartNew();
            var result = new CheckResult
            {
                Kind = "Check",
                Name = endpoint.Name
            };

            try
            {
                var request = new HttpRequestMessage(new HttpMethod(endpoint.Method.ToString().ToUpperInvariant()), endpoint.Url);
                foreach (var header in endpoint.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                if (!string.IsNullOrWhiteSpace(endpoint.Body))
                {
                    request.Content = new StringContent(endpoint.Body);
                }

                var response = await _provider.Client.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                sw.Stop();

                var errors = new List<string>();
                if (endpoint.Asserts.StatusCodeEquals.HasValue && (int)response.StatusCode != endpoint.Asserts.StatusCodeEquals.Value)
                {
                    errors.Add($"Status {(int)response.StatusCode}");
                }

                if (endpoint.Asserts.MaxLatencyMs.HasValue && sw.Elapsed.TotalMilliseconds > endpoint.Asserts.MaxLatencyMs.Value)
                {
                    errors.Add("Latency exceeded");
                }

                foreach (var expected in endpoint.Asserts.HeaderContains)
                {
                    if (!response.Headers.TryGetValues(expected.Key, out var values) || !values.Any(v => v.Contains(expected.Value, StringComparison.OrdinalIgnoreCase)))
                    {
                        errors.Add($"Header {expected.Key} missing");
                    }
                }

                if (!string.IsNullOrWhiteSpace(endpoint.Asserts.BodyContains) && !body.Contains(endpoint.Asserts.BodyContains, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Body missing substring");
                }

                if (!string.IsNullOrWhiteSpace(endpoint.Asserts.JsonKeyExists))
                {
                    if (!JsonPathExists(body, endpoint.Asserts.JsonKeyExists))
                    {
                        errors.Add("JSON key missing");
                    }
                }

                if (!string.IsNullOrWhiteSpace(endpoint.Asserts.JsonValueEqualsPath))
                {
                    if (!JsonValueEquals(body, endpoint.Asserts.JsonValueEqualsPath!, endpoint.Asserts.JsonValueEqualsExpected))
                    {
                        errors.Add("JSON value mismatch");
                    }
                }

                result = result with
                {
                    Success = errors.Count == 0,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = errors.Count == 0 ? null : string.Join("; ", errors),
                    ErrorType = errors.Count == 0 ? null : "Assert"
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                result = result with
                {
                    Success = false,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ErrorMessage = ex.Message,
                    ErrorType = ex.GetType().Name
                };
            }

            results.Add(result);
            ctx.Progress.Report(new ProgressUpdate(results.Count, s.Endpoints.Count, endpoint.Name));
        }

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = start,
            FinishedAt = ctx.Now,
            Status = TestStatus.Completed,
            AppVersion = AppVersionProvider.GetVersion(),
            OsDescription = Environment.OSVersion.ToString(),
            SettingsSnapshot = JsonSerializer.SerializeToElement(settings),
            Results = results
        };
    }

    private static bool JsonPathExists(string json, string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return TryNavigate(doc.RootElement, path, out _);
        }
        catch
        {
            return false;
        }
    }

    private static bool JsonValueEquals(string json, string path, string? expected)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!TryNavigate(doc.RootElement, path, out var element))
            {
                return false;
            }

            return element.ToString() == (expected ?? string.Empty);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryNavigate(JsonElement element, string path, out JsonElement result)
    {
        result = element;
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Contains('['))
            {
                var name = part[..part.IndexOf('[')];
                var indexPart = part[(part.IndexOf('[') + 1)..part.IndexOf(']')];
                if (!result.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                if (!int.TryParse(indexPart, out var index) || index >= array.GetArrayLength())
                {
                    return false;
                }

                result = array[index];
            }
            else
            {
                if (!result.TryGetProperty(part, out var next))
                {
                    return false;
                }

                result = next;
            }
        }

        return true;
    }
}

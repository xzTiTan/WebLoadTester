using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Http;

namespace WebLoadTester.Modules.HttpFunctional;

public sealed class HttpFunctionalModule : ITestModule
{
    private readonly HttpClientProvider _clientProvider = new();

    public string Id => "http-functional";
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

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpFunctionalSettings)settings;
        var results = new List<TestResult>();
        var client = _clientProvider.Client;
        client.Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds);

        var completed = 0;
        foreach (var endpoint in s.Endpoints)
        {
            ct.ThrowIfCancellationRequested();
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
                    request.Content = new StringContent(endpoint.Body, Encoding.UTF8, "application/json");
                }
                using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                sw.Stop();

                var success = ValidateResponse(endpoint.Asserts, response, body, sw.ElapsedMilliseconds, out var error);
                results.Add(new CheckResult(endpoint.Name, success, error, sw.Elapsed.TotalMilliseconds, (int)response.StatusCode, error));
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new CheckResult(endpoint.Name, false, ex.Message, sw.Elapsed.TotalMilliseconds, null, ex.Message));
            }
            finally
            {
                completed++;
                ctx.Progress.Report(new ProgressUpdate(completed, s.Endpoints.Count));
            }
        }

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            FinishedAt = ctx.Now,
            Status = TestStatus.Completed,
            Results = results
        };
    }

    private static bool ValidateResponse(HttpAsserts asserts, HttpResponseMessage response, string body, long latencyMs, out string? error)
    {
        if (asserts.StatusCodeEquals.HasValue && (int)response.StatusCode != asserts.StatusCodeEquals.Value)
        {
            error = $"Expected status {asserts.StatusCodeEquals} but got {(int)response.StatusCode}";
            return false;
        }
        if (asserts.MaxLatencyMs.HasValue && latencyMs > asserts.MaxLatencyMs.Value)
        {
            error = $"Latency {latencyMs}ms exceeds {asserts.MaxLatencyMs}";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(asserts.HeaderContainsKey))
        {
            if (!response.Headers.TryGetValues(asserts.HeaderContainsKey, out var values))
            {
                error = $"Header {asserts.HeaderContainsKey} missing";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(asserts.HeaderContainsValue) &&
                !values.Any(v => v.Contains(asserts.HeaderContainsValue, StringComparison.OrdinalIgnoreCase)))
            {
                error = $"Header {asserts.HeaderContainsKey} does not contain {asserts.HeaderContainsValue}";
                return false;
            }
        }
        if (!string.IsNullOrWhiteSpace(asserts.BodyContains) && !body.Contains(asserts.BodyContains, StringComparison.OrdinalIgnoreCase))
        {
            error = $"Body does not contain '{asserts.BodyContains}'";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(asserts.JsonKeyExists) || !string.IsNullOrWhiteSpace(asserts.JsonValueEqualsPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (!string.IsNullOrWhiteSpace(asserts.JsonKeyExists) &&
                    !TryGetJsonElement(doc.RootElement, asserts.JsonKeyExists!, out _))
                {
                    error = $"Json key {asserts.JsonKeyExists} not found";
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(asserts.JsonValueEqualsPath))
                {
                    if (!TryGetJsonElement(doc.RootElement, asserts.JsonValueEqualsPath!, out var elem))
                    {
                        error = $"Json path {asserts.JsonValueEqualsPath} not found";
                        return false;
                    }
                    var actual = elem.ValueKind switch
                    {
                        JsonValueKind.String => elem.GetString(),
                        JsonValueKind.Number => elem.GetRawText(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => elem.GetRawText()
                    };
                    if (!string.Equals(actual, asserts.JsonValueEqualsExpected, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"Json value mismatch: expected {asserts.JsonValueEqualsExpected}, got {actual}";
                        return false;
                    }
                }
            }
            catch (JsonException ex)
            {
                error = $"Invalid JSON response: {ex.Message}";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryGetJsonElement(JsonElement element, string path, out JsonElement result)
    {
        result = element;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = part;
            var index = -1;
            if (part.Contains('['))
            {
                var left = part.IndexOf('[', StringComparison.Ordinal);
                var right = part.IndexOf(']', StringComparison.Ordinal);
                name = part[..left];
                if (right > left && int.TryParse(part[(left + 1)..right], out var parsed))
                {
                    index = parsed;
                }
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                if (!result.TryGetProperty(name, out result))
                {
                    return false;
                }
            }

            if (index >= 0)
            {
                if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() <= index)
                {
                    return false;
                }
                result = result[index];
            }
        }

        return true;
    }
}

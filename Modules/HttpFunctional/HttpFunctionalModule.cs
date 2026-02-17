using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
    public string Description => "Проверяет HTTP-эндпоинты на ожидаемые статусы и assert-условия MVP.";
    public TestFamily Family => TestFamily.HttpTesting;
    public Type SettingsType => typeof(HttpFunctionalSettings);

    public object CreateDefaultSettings()
    {
        return new HttpFunctionalSettings
        {
            Endpoints = new List<HttpFunctionalEndpoint>
            {
                new() { Name = "Endpoint 1", Path = "/", ExpectedStatusCode = 200 }
            }
        };
    }

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not HttpFunctionalSettings s)
        {
            errors.Add("Неверный тип настроек HTTP functional.");
            return errors;
        }

        foreach (var endpoint in s.Endpoints)
        {
            endpoint.NormalizeLegacy();
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

            if (!endpoint.ExpectedStatusCode.HasValue)
            {
                errors.Add($"{prefix}: ExpectedStatusCode обязателен.");
            }
            else if (endpoint.ExpectedStatusCode is < 100 or > 599)
            {
                errors.Add($"{prefix}: ExpectedStatusCode должен быть в диапазоне 100..599.");
            }

            foreach (var headerRule in endpoint.RequiredHeaders.Where(v => !string.IsNullOrWhiteSpace(v)))
            {
                var header = headerRule.Trim();
                var colonIndex = header.IndexOf(':');
                if (colonIndex == 0)
                {
                    errors.Add($"{prefix}: RequiredHeaders содержит некорректное значение '{headerRule}'.");
                }
            }

            foreach (var jsonRule in endpoint.JsonFieldEquals.Where(v => !string.IsNullOrWhiteSpace(v)))
            {
                if (!jsonRule.Contains('='))
                {
                    errors.Add($"{prefix}: JsonFieldEquals должен быть в формате path=value.");
                }
            }
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (HttpFunctionalSettings)settings;
        foreach (var endpoint in s.Endpoints)
        {
            endpoint.NormalizeLegacy();
        }

        var result = new ModuleResult();
        ctx.Log.Info($"[HttpFunctional] BaseUrl={s.BaseUrl}, endpoints={s.Endpoints.Count}");

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
                var body = await response.Content.ReadAsStringAsync(ct);
                sw.Stop();

                statusCode = (int)response.StatusCode;

                if (statusCode != endpoint.ExpectedStatusCode)
                {
                    success = false;
                    message = $"Ожидали код {endpoint.ExpectedStatusCode}, получили {statusCode}.";
                    errorKind = "AssertFailed";
                }

                if (success)
                {
                    var headerAssert = CheckRequiredHeaders(response, endpoint.RequiredHeaders);
                    if (!headerAssert.Ok)
                    {
                        success = false;
                        message = headerAssert.Message;
                        errorKind = "AssertFailed";
                    }
                }

                if (success && !string.IsNullOrWhiteSpace(endpoint.BodyContains) && !body.Contains(endpoint.BodyContains, StringComparison.Ordinal))
                {
                    success = false;
                    message = $"Ответ не содержит подстроку '{endpoint.BodyContains}'.";
                    errorKind = "AssertFailed";
                }

                if (success && endpoint.JsonFieldEquals.Count > 0)
                {
                    var jsonAssert = CheckJsonFieldEquals(body, endpoint.JsonFieldEquals);
                    if (!jsonAssert.Ok)
                    {
                        success = false;
                        message = jsonAssert.Message;
                        errorKind = "AssertFailed";
                    }
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

            ctx.Progress.Report(new ProgressUpdate(i + 1, s.Endpoints.Count, "HTTP функциональные проверки"));
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }

    public static (bool Ok, string Message) CheckJsonFieldEquals(string body, IReadOnlyList<string> rules)
    {
        if (rules.Count == 0)
        {
            return (true, "ok");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            foreach (var rawRule in rules.Where(v => !string.IsNullOrWhiteSpace(v)))
            {
                var splitIndex = rawRule.IndexOf('=');
                if (splitIndex <= 0)
                {
                    return (false, $"Некорректное правило JsonFieldEquals: {rawRule}.");
                }

                var path = rawRule[..splitIndex].Trim();
                var expected = rawRule[(splitIndex + 1)..].Trim();
                if (!TryGetByDotPath(document.RootElement, path, out var actual))
                {
                    return (false, $"JSON path не найден: {path}.");
                }

                var actualText = actual.ValueKind switch
                {
                    JsonValueKind.String => actual.GetString() ?? string.Empty,
                    JsonValueKind.Number => actual.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => "null",
                    _ => actual.GetRawText()
                };

                if (!string.Equals(actualText, expected, StringComparison.Ordinal))
                {
                    return (false, $"JSON path {path}: ожидали '{expected}', получили '{actualText}'.");
                }
            }

            return (true, "ok");
        }
        catch (JsonException)
        {
            return (false, "Ответ не является валидным JSON.");
        }
    }

    public static bool TryGetByDotPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (var segment in ParsePath(path))
        {
            if (segment.IsIndex)
            {
                if (value.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var index = segment.Index;
                if (index < 0 || index >= value.GetArrayLength())
                {
                    return false;
                }

                value = value[index];
                continue;
            }

            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment.Name!, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<PathSegment> ParsePath(string path)
    {
        var tokens = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            foreach (var segment in ParseToken(token))
            {
                yield return segment;
            }
        }
    }

    private static IEnumerable<PathSegment> ParseToken(string token)
    {
        if (int.TryParse(token, out var standaloneIndex))
        {
            yield return PathSegment.ForIndex(standaloneIndex);
            yield break;
        }

        var bracketStart = token.IndexOf('[');
        if (bracketStart < 0)
        {
            yield return PathSegment.ForName(token);
            yield break;
        }

        var property = token[..bracketStart];
        if (!string.IsNullOrWhiteSpace(property))
        {
            yield return PathSegment.ForName(property);
        }

        var remainder = token[bracketStart..];
        while (!string.IsNullOrWhiteSpace(remainder))
        {
            if (!remainder.StartsWith("[", StringComparison.Ordinal))
            {
                yield break;
            }

            var close = remainder.IndexOf(']');
            if (close <= 1)
            {
                yield break;
            }

            var indexRaw = remainder[1..close];
            if (int.TryParse(indexRaw, out var index))
            {
                yield return PathSegment.ForIndex(index);
            }

            remainder = remainder[(close + 1)..];
        }
    }

    private static (bool Ok, string Message) CheckRequiredHeaders(HttpResponseMessage response, IReadOnlyList<string> rules)
    {
        foreach (var rawRule in rules.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            var rule = rawRule.Trim();
            var splitIndex = rule.IndexOf(':');
            if (splitIndex > 0)
            {
                var headerName = rule[..splitIndex].Trim();
                var expectedValue = rule[(splitIndex + 1)..].Trim();
                if (!TryGetHeaderValue(response, headerName, out var actual) ||
                    !actual.Contains(expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Заголовок {headerName} не содержит значение '{expectedValue}'.");
                }
                continue;
            }

            var keyOnly = splitIndex < 0 ? rule : rule[..splitIndex];
            if (!TryGetHeaderValue(response, keyOnly, out _))
            {
                return (false, $"Отсутствует обязательный заголовок {keyOnly}.");
            }
        }

        return (true, "ok");
    }

    private static bool TryGetHeaderValue(HttpResponseMessage response, string headerName, out string value)
    {
        value = string.Empty;
        if (response.Headers.TryGetValues(headerName, out var values))
        {
            value = string.Join(';', values);
            return true;
        }

        if (response.Content.Headers.TryGetValues(headerName, out var contentValues))
        {
            value = string.Join(';', contentValues);
            return true;
        }

        return false;
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

    private readonly record struct PathSegment(string? Name, int Index, bool IsIndex)
    {
        public static PathSegment ForName(string name) => new(name, -1, false);
        public static PathSegment ForIndex(int index) => new(null, index, true);
    }
}

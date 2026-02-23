using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;

namespace WebLoadTester.Modules.HttpFunctional;

/// <summary>
/// Настройки функциональных HTTP-проверок.
/// </summary>
public class HttpFunctionalSettings
{
    public string BaseUrl { get; set; } = "https://www.google.com/";
    public List<HttpFunctionalEndpoint> Endpoints { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 15;
}

/// <summary>
/// Описание одного HTTP-эндпоинта и assert-условий.
/// </summary>
public class HttpFunctionalEndpoint
{
    public static IReadOnlyList<string> MethodOptions { get; } = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = "/";
    public string Method { get; set; } = HttpMethod.Get.Method;
    public int? ExpectedStatusCode { get; set; }
    public List<string> RequiredHeaders { get; set; } = new();
    public string? BodyContains { get; set; }
    public List<string> JsonFieldEquals { get; set; } = new();


    [JsonIgnore]
    public string RequiredHeadersText
    {
        get => string.Join(";", RequiredHeaders);
        set => RequiredHeaders = SplitToList(value);
    }

    [JsonIgnore]
    public string JsonFieldEqualsText
    {
        get => string.Join(";", JsonFieldEquals);
        set => JsonFieldEquals = SplitToList(value);
    }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? LegacyData { get; set; }

    /// <summary>
    /// Нормализует legacy-поля в каноничную модель.
    /// </summary>
    public void NormalizeLegacy()
    {
        if (ExpectedStatusCode is null && TryGetLegacyInt("statusCodeEquals", out var statusCode))
        {
            ExpectedStatusCode = statusCode;
        }

        if (RequiredHeaders.Count == 0)
        {
            if (TryGetLegacyStringDictionary("headers", out var headers))
            {
                foreach (var pair in headers)
                {
                    RequiredHeaders.Add(string.IsNullOrWhiteSpace(pair.Value)
                        ? pair.Key
                        : $"{pair.Key}:{pair.Value}");
                }
            }

            if (TryGetLegacyString("headerContainsKey", out var headerKey) && !string.IsNullOrWhiteSpace(headerKey))
            {
                if (TryGetLegacyString("headerContainsValue", out var headerValue) && !string.IsNullOrWhiteSpace(headerValue))
                {
                    RequiredHeaders.Add($"{headerKey}:{headerValue}");
                }
                else
                {
                    RequiredHeaders.Add(headerKey);
                }
            }
        }
    }

    private bool TryGetLegacyInt(string key, out int value)
    {
        value = 0;
        if (LegacyData == null || !LegacyData.TryGetValue(key, out var element))
        {
            return false;
        }

        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetInt32(out value);
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return int.TryParse(element.GetString(), out value);
        }

        return false;
    }

    private bool TryGetLegacyString(string key, out string value)
    {
        value = string.Empty;
        if (LegacyData == null || !LegacyData.TryGetValue(key, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return true;
    }

    private static List<string> SplitToList(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private bool TryGetLegacyStringDictionary(string key, out Dictionary<string, string> dictionary)
    {
        dictionary = new Dictionary<string, string>();
        if (LegacyData == null || !LegacyData.TryGetValue(key, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            dictionary[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return dictionary.Count > 0;
    }
}

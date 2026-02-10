using System.Collections.Generic;
using System.Net.Http;

namespace WebLoadTester.Modules.HttpFunctional;

/// <summary>
/// Настройки функциональных HTTP-проверок.
/// </summary>
public class HttpFunctionalSettings
{
    public string BaseUrl { get; set; } = "https://example.com";
    public List<HttpEndpoint> Endpoints { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 15;
}

/// <summary>
/// Описание одного HTTP-эндпоинта и ожидаемых условий.
/// </summary>
public class HttpEndpoint
{
    public static IReadOnlyList<string> MethodOptions { get; } = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = "/";
    public string Method { get; set; } = HttpMethod.Get.Method;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public int? StatusCodeEquals { get; set; }
    public int? MaxLatencyMs { get; set; }
    public string? HeaderContainsKey { get; set; }
    public string? HeaderContainsValue { get; set; }
    public string? BodyContains { get; set; }
}

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WebLoadTester.Modules.HttpPerformance;

/// <summary>
/// Настройки нагрузочного теста HTTP.
/// </summary>
public class HttpPerformanceSettings
{
    public string BaseUrl { get; set; } = "https://example.com";
    public List<HttpPerformanceEndpoint> Endpoints { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 10;
}

public class HttpPerformanceEndpoint
{
    public static IReadOnlyList<string> MethodOptions { get; } =
        new ReadOnlyCollection<string>(new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" });

    public string Name { get; set; } = "Endpoint";
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/";
    public int? ExpectedStatusCode { get; set; }
}

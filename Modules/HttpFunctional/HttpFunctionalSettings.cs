using System.Collections.Generic;

namespace WebLoadTester.Modules.HttpFunctional;

public sealed class HttpFunctionalSettings
{
    public List<HttpEndpoint> Endpoints { get; set; } = new()
    {
        new HttpEndpoint { Name = "Example", Url = "https://example.com", Method = "GET" }
    };
    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class HttpEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public HttpAsserts Asserts { get; set; } = new();
}

public sealed class HttpAsserts
{
    public int? StatusCodeEquals { get; set; }
    public int? MaxLatencyMs { get; set; }
    public string? HeaderContainsKey { get; set; }
    public string? HeaderContainsValue { get; set; }
    public string? BodyContains { get; set; }
    public string? JsonKeyExists { get; set; }
    public string? JsonValueEqualsPath { get; set; }
    public string? JsonValueEqualsExpected { get; set; }
}

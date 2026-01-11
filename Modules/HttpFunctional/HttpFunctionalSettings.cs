using System.Net.Http.Headers;

namespace WebLoadTester.Modules.HttpFunctional;

public sealed class HttpFunctionalSettings
{
    public List<HttpEndpoint> Endpoints { get; set; } = new()
    {
        new HttpEndpoint { Name = "Example", Url = "https://example.com", Method = "GET" }
    };
    public int TimeoutSeconds { get; set; } = 20;
}

public sealed class HttpEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public HttpAssertSettings Asserts { get; set; } = new();
}

public sealed class HttpAssertSettings
{
    public int? StatusCodeEquals { get; set; }
    public int? MaxLatencyMs { get; set; }
    public Dictionary<string, string> HeaderContains { get; set; } = new();
    public string? BodyContains { get; set; }
    public string? JsonKeyExists { get; set; }
    public string? JsonValueEqualsPath { get; set; }
    public string? JsonValueEqualsExpected { get; set; }
}

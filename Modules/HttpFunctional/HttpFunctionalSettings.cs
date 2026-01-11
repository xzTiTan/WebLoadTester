using System.Collections.Generic;
using System.Net.Http;

namespace WebLoadTester.Modules.HttpFunctional;

public class HttpFunctionalSettings
{
    public List<HttpEndpoint> Endpoints { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 15;
}

public class HttpEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public HttpMethod Method { get; set; } = HttpMethod.Get;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public int? StatusCodeEquals { get; set; }
    public int? MaxLatencyMs { get; set; }
    public string? HeaderContainsKey { get; set; }
    public string? HeaderContainsValue { get; set; }
    public string? BodyContains { get; set; }
}

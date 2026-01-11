namespace WebLoadTester.Modules.HttpFunctional;

public sealed class HttpFunctionalSettings
{
    public List<HttpEndpoint> Endpoints { get; set; } = new()
    {
        new HttpEndpoint { Name = "Example", Url = "https://example.com", Method = HttpMethodType.Get }
    };

    public string EndpointsText
    {
        get => string.Join(Environment.NewLine, Endpoints.Select(e => e.Url));
        set => Endpoints = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(url => new HttpEndpoint { Name = url, Url = url, Method = HttpMethodType.Get }).ToList();
    }
}

public sealed class HttpEndpoint
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public HttpMethodType Method { get; set; } = HttpMethodType.Get;
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? Body { get; set; }
    public HttpAsserts Asserts { get; set; } = new();
}

public sealed class HttpAsserts
{
    public int? StatusCodeEquals { get; set; }
    public int? MaxLatencyMs { get; set; }
    public Dictionary<string, string> HeaderContains { get; set; } = new();
    public string? BodyContains { get; set; }
    public string? JsonKeyExists { get; set; }
    public string? JsonValueEqualsPath { get; set; }
    public string? JsonValueEqualsExpected { get; set; }
}

public enum HttpMethodType
{
    Get,
    Post,
    Put,
    Delete
}

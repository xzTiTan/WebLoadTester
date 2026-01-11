namespace WebLoadTester.Modules.HttpPerformance;

public sealed class HttpPerformanceSettings
{
    public string Url { get; set; } = "https://example.com";
    public HttpMethodType Method { get; set; } = HttpMethodType.Get;
    public int TotalRequests { get; set; } = 20;
    public int Concurrency { get; set; } = 5;
    public int? RpsLimit { get; set; } = 10;
}

public enum HttpMethodType
{
    Get,
    Post
}

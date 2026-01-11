using System.Net.Http;

namespace WebLoadTester.Modules.HttpPerformance;

public class HttpPerformanceSettings
{
    public string Url { get; set; } = "https://example.com";
    public HttpMethod Method { get; set; } = HttpMethod.Get;
    public int TotalRequests { get; set; } = 20;
    public int Concurrency { get; set; } = 5;
    public int? RpsLimit { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 10;
}

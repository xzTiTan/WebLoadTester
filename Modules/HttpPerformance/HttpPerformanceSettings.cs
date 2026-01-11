namespace WebLoadTester.Modules.HttpPerformance;

public sealed class HttpPerformanceSettings
{
    public string Url { get; set; } = "https://example.com";
    public string Method { get; set; } = "GET";
    public int TotalRequests { get; set; } = 50;
    public int Concurrency { get; set; } = 5;
    public int? RpsLimit { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}

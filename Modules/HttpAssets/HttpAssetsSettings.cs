namespace WebLoadTester.Modules.HttpAssets;

public sealed class HttpAssetsSettings
{
    public List<string> Assets { get; set; } = new() { "https://example.com" };
    public string? ExpectedContentType { get; set; }
    public int? MaxSizeBytes { get; set; }
    public int? MaxLatencyMs { get; set; }
    public int TimeoutSeconds { get; set; } = 20;
}

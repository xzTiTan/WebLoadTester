using System.Collections.Generic;

namespace WebLoadTester.Modules.HttpAssets;

public sealed class HttpAssetsSettings
{
    public List<AssetEntry> Assets { get; set; } = new()
    {
        new AssetEntry { Url = "https://example.com" }
    };
    public string? ExpectedContentType { get; set; }
    public int? MaxSizeBytes { get; set; }
    public int? MaxLatencyMs { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
}

public sealed class AssetEntry
{
    public string Url { get; set; } = string.Empty;
}

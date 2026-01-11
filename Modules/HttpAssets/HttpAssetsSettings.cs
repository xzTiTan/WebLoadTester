using System.Collections.Generic;

namespace WebLoadTester.Modules.HttpAssets;

public class HttpAssetsSettings
{
    public List<AssetItem> Assets { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 10;
}

public class AssetItem
{
    public string Url { get; set; } = string.Empty;
    public string? ExpectedContentType { get; set; }
    public int? MaxSizeBytes { get; set; }
    public int? MaxLatencyMs { get; set; }
}

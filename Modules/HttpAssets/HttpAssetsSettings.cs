using System.Collections.Generic;

namespace WebLoadTester.Modules.HttpAssets;

/// <summary>
/// Настройки проверки доступности и характеристик ассетов.
/// </summary>
public class HttpAssetsSettings
{
    public string BaseUrl { get; set; } = "https://example.com";
    public List<AssetItem> Assets { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Описание одного ассета для проверки.
/// </summary>
public class AssetItem
{
    public string Path { get; set; } = "/";
    public string? ExpectedContentType { get; set; }
    public int? MaxSizeBytes { get; set; }
    public int? MaxLatencyMs { get; set; }
}

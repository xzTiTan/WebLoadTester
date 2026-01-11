using System.Collections.Generic;

namespace WebLoadTester.Modules.HttpAssets;

/// <summary>
/// Настройки проверки доступности и характеристик ассетов.
/// </summary>
public class HttpAssetsSettings
{
    public List<AssetItem> Assets { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Описание одного ассета для проверки.
/// </summary>
public class AssetItem
{
    public string Url { get; set; } = string.Empty;
    public string? ExpectedContentType { get; set; }
    public int? MaxSizeBytes { get; set; }
    public int? MaxLatencyMs { get; set; }
}

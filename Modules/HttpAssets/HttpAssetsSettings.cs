using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    public string Url { get; set; } = "https://example.com";
    public string? Name { get; set; }
    public string? ExpectedContentType { get; set; }
    public int? MaxSizeKb { get; set; }
    public int? MaxLatencyMs { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? LegacyData { get; set; }

    public void NormalizeLegacy()
    {
        if (!MaxSizeKb.HasValue && LegacyData != null && LegacyData.TryGetValue("maxSizeBytes", out var bytesElement))
        {
            long? maxBytes = bytesElement.ValueKind switch
            {
                JsonValueKind.Number when bytesElement.TryGetInt64(out var n) => n,
                JsonValueKind.String when long.TryParse(bytesElement.GetString(), out var s) => s,
                _ => null
            };

            if (maxBytes.HasValue && maxBytes.Value > 0)
            {
                MaxSizeKb = (int)Math.Ceiling(maxBytes.Value / 1024d);
            }
        }
    }
}

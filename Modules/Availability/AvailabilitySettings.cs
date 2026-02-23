using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebLoadTester.Modules.Availability;

/// <summary>
/// Настройки проверки доступности целевого ресурса.
/// </summary>
public class AvailabilitySettings
{
    public string CheckType { get; set; } = "HTTP";
    public string Url { get; set; } = "https://www.google.com/";
    public string Host { get; set; } = "www.google.com";
    public int Port { get; set; } = 443;
    public int TimeoutMs { get; set; } = 10000;

    [JsonIgnore]
    public string Target
    {
        get => CheckType.Equals("TCP", System.StringComparison.OrdinalIgnoreCase) ? $"{Host}:{Port}" : Url;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (value.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase))
            {
                CheckType = "HTTP";
                Url = value;
                return;
            }

            CheckType = "TCP";
            var parts = value.Split(':', 2);
            Host = parts[0];
            if (parts.Length == 2 && int.TryParse(parts[1], out var parsedPort))
            {
                Port = parsedPort;
            }
        }
    }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? LegacyData { get; set; }

    public void NormalizeLegacy()
    {
        if (LegacyData == null)
        {
            return;
        }

        if (LegacyData.TryGetValue("targetType", out var typeElement) && typeElement.ValueKind == JsonValueKind.String)
        {
            CheckType = typeElement.GetString()?.Equals("Tcp", System.StringComparison.OrdinalIgnoreCase) == true ? "TCP" : "HTTP";
        }

        if (LegacyData.TryGetValue("target", out var targetElement) && targetElement.ValueKind == JsonValueKind.String)
        {
            var target = targetElement.GetString() ?? string.Empty;
            if (CheckType == "HTTP")
            {
                Url = target;
            }
            else
            {
                var parts = target.Split(':', 2);
                Host = parts[0];
                if (parts.Length == 2 && int.TryParse(parts[1], out var parsed))
                {
                    Port = parsed;
                }
            }
        }
    }
}

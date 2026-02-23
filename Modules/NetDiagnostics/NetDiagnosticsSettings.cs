using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebLoadTester.Modules.NetDiagnostics;

/// <summary>
/// Настройки сетевой диагностики (DNS/TCP/TLS).
/// </summary>
public class NetDiagnosticsSettings
{
    public string Hostname { get; set; } = "www.google.com";
    public bool CheckDns { get; set; } = true;
    public bool CheckTcp { get; set; } = true;
    public bool CheckTls { get; set; } = true;
    public bool UseAutoPorts { get; set; } = false;
    public List<DiagnosticPort> Ports { get; set; } = new() { new() { Port = 80 }, new() { Port = 443 } };

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? LegacyData { get; set; }

    public void NormalizeLegacy()
    {
        if (LegacyData == null)
        {
            return;
        }

        if (LegacyData.TryGetValue("enableDns", out var enableDns))
        {
            CheckDns = ReadBool(enableDns, CheckDns);
        }

        if (LegacyData.TryGetValue("enableTcp", out var enableTcp))
        {
            CheckTcp = ReadBool(enableTcp, CheckTcp);
        }

        if (LegacyData.TryGetValue("enableTls", out var enableTls))
        {
            CheckTls = ReadBool(enableTls, CheckTls);
        }

        if (LegacyData.TryGetValue("autoPortsByScheme", out var autoPortsByScheme))
        {
            UseAutoPorts = ReadBool(autoPortsByScheme, UseAutoPorts);
        }

        if (Ports.Count == 0 && LegacyData.TryGetValue("ports", out var legacyPorts) && legacyPorts.ValueKind == JsonValueKind.Array)
        {
            Ports = legacyPorts.EnumerateArray()
                .Select(e => e.TryGetInt32(out var p) ? p : 0)
                .Where(p => p is >= 1 and <= 65535)
                .Select(p => new DiagnosticPort { Port = p, Protocol = "Tcp" })
                .ToList();
        }
    }

    private static bool ReadBool(JsonElement element, bool fallback)
    {
        if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
        {
            return element.GetBoolean();
        }

        if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var parsed))
        {
            return parsed;
        }

        return fallback;
    }
}

public class DiagnosticPort
{
    public int Port { get; set; } = 443;
    public string Protocol { get; set; } = "Tcp";
}

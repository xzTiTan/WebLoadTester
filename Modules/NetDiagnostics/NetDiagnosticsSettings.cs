namespace WebLoadTester.Modules.NetDiagnostics;

public sealed class NetDiagnosticsSettings
{
    public string Hostname { get; set; } = "example.com";
    public List<int> Ports { get; set; } = new() { 80, 443 };
    public bool EnableDns { get; set; } = true;
    public bool EnableTcp { get; set; } = true;
    public bool EnableTls { get; set; } = true;

    public string PortsText
    {
        get => string.Join(",", Ports);
        set => Ports = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => int.TryParse(p.Trim(), out var port) ? port : 0)
            .Where(p => p > 0).ToList();
    }
}

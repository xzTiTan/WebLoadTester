using System.Collections.Generic;

namespace WebLoadTester.Modules.NetDiagnostics;

public class NetDiagnosticsSettings
{
    public string Hostname { get; set; } = "example.com";
    public List<int> Ports { get; set; } = new() { 80, 443 };
    public bool EnableDns { get; set; } = true;
    public bool EnableTcp { get; set; } = true;
    public bool EnableTls { get; set; } = true;
}

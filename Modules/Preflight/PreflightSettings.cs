namespace WebLoadTester.Modules.Preflight;

/// <summary>
/// Настройки предварительных проверок доступности цели.
/// </summary>
public class PreflightSettings
{
    public string Target { get; set; } = "https://example.com";
    public bool CheckDns { get; set; } = true;
    public bool CheckTcp { get; set; } = true;
    public bool CheckTls { get; set; } = true;
    public bool CheckHttp { get; set; } = true;
}

namespace WebLoadTester.Modules.SecurityBaseline;

/// <summary>
/// Настройки проверки базовых требований безопасности.
/// </summary>
public class SecurityBaselineSettings
{
    public string Url { get; set; } = "https://example.com";
    public bool CheckHeaders { get; set; } = true;
    public bool CheckRedirectHttpToHttps { get; set; } = false;
    public bool CheckTlsExpiry { get; set; } = false;
}

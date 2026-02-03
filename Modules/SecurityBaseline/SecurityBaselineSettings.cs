namespace WebLoadTester.Modules.SecurityBaseline;

/// <summary>
/// Настройки проверки базовых требований безопасности.
/// </summary>
public class SecurityBaselineSettings
{
    public string Url { get; set; } = "https://example.com";
    public bool CheckHsts { get; set; } = true;
    public bool CheckContentTypeOptions { get; set; } = true;
    public bool CheckFrameOptions { get; set; } = true;
    public bool CheckContentSecurityPolicy { get; set; } = true;
    public bool CheckReferrerPolicy { get; set; } = true;
    public bool CheckPermissionsPolicy { get; set; } = true;
    public bool CheckRedirectHttpToHttps { get; set; } = true;
}

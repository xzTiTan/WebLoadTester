namespace WebLoadTester.Modules.SecurityBaseline;

public sealed class SecurityBaselineSettings
{
    public string Url { get; set; } = "https://example.com";
    public bool CheckRedirectHttpToHttps { get; set; } = true;
    public bool CheckTlsExpiry { get; set; } = true;
}

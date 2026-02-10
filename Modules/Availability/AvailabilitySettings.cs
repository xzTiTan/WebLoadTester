namespace WebLoadTester.Modules.Availability;

/// <summary>
/// Настройки проверки доступности целевого ресурса.
/// </summary>
public class AvailabilitySettings
{
    public string Target { get; set; } = "https://example.com";
    public string TargetType { get; set; } = "Http";
    public int IntervalSeconds { get; set; } = 0;
    public int TimeoutMs { get; set; } = 5000;
}

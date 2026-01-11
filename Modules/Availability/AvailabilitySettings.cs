namespace WebLoadTester.Modules.Availability;

/// <summary>
/// Настройки проверки доступности целевого ресурса.
/// </summary>
public class AvailabilitySettings
{
    public string Target { get; set; } = "https://example.com";
    public string TargetType { get; set; } = "Http";
    public int IntervalSeconds { get; set; } = 5;
    public int DurationMinutes { get; set; } = 1;
    public int TimeoutMs { get; set; } = 5000;
    public int FailThreshold { get; set; } = 3;
}

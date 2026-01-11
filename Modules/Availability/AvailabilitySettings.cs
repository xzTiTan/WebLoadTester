using System;

namespace WebLoadTester.Modules.Availability;

public enum AvailabilityTargetType
{
    Http,
    Tcp
}

public sealed class AvailabilitySettings
{
    public AvailabilityTargetType TargetType { get; set; } = AvailabilityTargetType.Http;
    public string Target { get; set; } = "https://example.com";
    public int IntervalSeconds { get; set; } = 5;
    public int DurationMinutes { get; set; } = 1;
    public int TimeoutMs { get; set; } = 5000;
    public int? FailThreshold { get; set; }

    public AvailabilityTargetType[] TargetTypeValues { get; } = Enum.GetValues<AvailabilityTargetType>();
}

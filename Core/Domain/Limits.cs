namespace WebLoadTester.Core.Domain;

public record Limits
{
    public int MaxUiConcurrency { get; init; } = 50;
    public int MaxHttpConcurrency { get; init; } = 50;
    public int MaxRps { get; init; } = 100;
    public int MinAvailabilityIntervalSeconds { get; init; } = 5;
    public int MaxAvailabilityDurationMinutes { get; init; } = 30;
}

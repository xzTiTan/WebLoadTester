namespace WebLoadTester.Core.Domain;

public sealed record Limits(
    int MaxUiConcurrency,
    int MaxHttpConcurrency,
    int MaxRps,
    int MinAvailabilityIntervalSeconds,
    int MaxAvailabilityDurationMinutes)
{
    public static Limits Default => new(50, 50, 100, 5, 30);
}

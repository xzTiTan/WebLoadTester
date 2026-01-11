using System;

namespace WebLoadTester.Core.Domain;

public abstract record ResultBase(string Kind)
{
    public bool Success { get; init; }
    public double DurationMs { get; init; }
    public string? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }
}

public record RunResult(string Name) : ResultBase("Run")
{
    public string? ScreenshotPath { get; init; }
}

public record CheckResult(string Name) : ResultBase("Check")
{
    public int? StatusCode { get; init; }
}

public record ProbeResult(string Name) : ResultBase("Probe")
{
    public string? Details { get; init; }
}

public record TimingResult(string Name) : ResultBase("Timing")
{
    public int Iteration { get; init; }
    public string? Url { get; init; }
}

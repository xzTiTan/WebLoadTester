using System;
using System.Text.Json;

namespace WebLoadTester.Core.Domain;

/// <summary>
/// Базовый результат проверки/прогона с общими полями.
/// </summary>
public abstract record ResultBase(string Kind)
{
    public bool Success { get; init; }
    public double DurationMs { get; init; }
    public string? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }
    public int WorkerId { get; init; }
    public int IterationIndex { get; init; }
    public int? ItemIndex { get; init; }
    public string? Severity { get; init; }
}

public record RunResult(string Name) : ResultBase("Run")
{
    public string? ScreenshotPath { get; init; }
    public string? DetailsJson { get; init; }
}

public record StepResult(string Name) : ResultBase("Step")
{
    public string? Action { get; init; }
    public string? Selector { get; init; }
    public string? ScreenshotPath { get; init; }
    public string? DetailsJson { get; init; }
}

public record CheckResult(string Name) : ResultBase("Check")
{
    public int? StatusCode { get; init; }
    public JsonElement? Metrics { get; init; }
}

public record EndpointResult(string Name) : ResultBase("Endpoint")
{
    public int? StatusCode { get; init; }
    public double LatencyMs { get; init; }
}

public record AssetResult(string Name) : ResultBase("Asset")
{
    public int? StatusCode { get; init; }
    public double LatencyMs { get; init; }
    public long Bytes { get; init; }
    public string? ContentType { get; init; }
}

public record ProbeResult(string Name) : ResultBase("Probe")
{
    public string? Details { get; init; }
}

public record TimingResult(string Name) : ResultBase("Timing")
{
    public int Iteration { get; init; }
    public string? Url { get; init; }
    public string? DetailsJson { get; init; }
}

public record PreflightResult(string Name) : ResultBase("PreflightCheck")
{
    public int? StatusCode { get; init; }
    public string? Details { get; init; }
    public JsonElement? Metrics { get; init; }
}

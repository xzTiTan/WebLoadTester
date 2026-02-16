using System;

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
}

/// <summary>
/// Результат прогона сценария UI.
/// </summary>
public record RunResult(string Name) : ResultBase("Run")
{
    public string? ScreenshotPath { get; init; }
    public string? DetailsJson { get; init; }
}

/// <summary>
/// Результат выполнения шага сценария.
/// </summary>
public record StepResult(string Name) : ResultBase("Step")
{
    public string? Action { get; init; }
    public string? Selector { get; init; }
    public string? ScreenshotPath { get; init; }
    public string? DetailsJson { get; init; }
}

/// <summary>
/// Результат проверки (например HTTP).
/// </summary>
public record CheckResult(string Name) : ResultBase("Check")
{
    public int? StatusCode { get; init; }
}

/// <summary>
/// Результат сетевого зонда (DNS/TCP/TLS).
/// </summary>
public record ProbeResult(string Name) : ResultBase("Probe")
{
    public string? Details { get; init; }
}

/// <summary>
/// Результат замера времени загрузки.
/// </summary>
public record TimingResult(string Name) : ResultBase("Timing")
{
    public int Iteration { get; init; }
    public string? Url { get; init; }
    public string? DetailsJson { get; init; }
}

/// <summary>
/// Результат preflight-проверки.
/// </summary>
public record PreflightResult(string Name) : ResultBase("PreflightCheck")
{
    public int? StatusCode { get; init; }
    public string? Details { get; init; }
}

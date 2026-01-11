using System;

namespace WebLoadTester.Core.Domain;

public enum ResultKind
{
    Run,
    Check,
    Probe
}

public abstract record TestResult(ResultKind Kind, string Name, bool Success, string? ErrorMessage, double DurationMs);

public sealed record RunResult(
    string Name,
    bool Success,
    string? ErrorMessage,
    double DurationMs,
    string? ScreenshotPath)
    : TestResult(ResultKind.Run, Name, Success, ErrorMessage, DurationMs);

public sealed record CheckResult(
    string Name,
    bool Success,
    string? ErrorMessage,
    double DurationMs,
    int? StatusCode,
    string? Details)
    : TestResult(ResultKind.Check, Name, Success, ErrorMessage, DurationMs);

public sealed record ProbeResult(
    string Name,
    bool Success,
    string? ErrorMessage,
    double DurationMs,
    string? Details)
    : TestResult(ResultKind.Probe, Name, Success, ErrorMessage, DurationMs);

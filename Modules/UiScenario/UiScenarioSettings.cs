using System.Collections.Generic;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Modules.UiScenario;

public sealed class UiScenarioSettings
{
    public string TargetUrl { get; set; } = "https://example.com";
    public int TotalRuns { get; set; } = 3;
    public int Concurrency { get; set; } = 1;
    public bool Headless { get; set; } = true;
    public List<UiStep> Steps { get; set; } = new()
    {
        new UiStep { Selector = "body", Action = UiStepAction.WaitForVisible }
    };
    public StepErrorPolicy StepErrorPolicy { get; set; } = StepErrorPolicy.StopRun;
    public bool ScreenshotAfterScenario { get; set; } = true;

    public IReadOnlyList<StepErrorPolicy> StepErrorPolicyValues => new[]
    {
        StepErrorPolicy.SkipStep,
        StepErrorPolicy.StopRun,
        StepErrorPolicy.StopAll
    };
}

public sealed class UiStep
{
    public string Selector { get; set; } = string.Empty;
    public UiStepAction Action { get; set; } = UiStepAction.WaitForVisible;
    public string? Text { get; set; }
    public int TimeoutMs { get; set; } = 5000;

    public IReadOnlyList<UiStepAction> ActionValues => new[]
    {
        UiStepAction.WaitForVisible,
        UiStepAction.Click,
        UiStepAction.FillText
    };
}

public enum UiStepAction
{
    WaitForVisible,
    Click,
    FillText
}

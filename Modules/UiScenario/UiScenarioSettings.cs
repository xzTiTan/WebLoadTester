using WebLoadTester.Core.Domain;

namespace WebLoadTester.Modules.UiScenario;

public sealed class UiScenarioSettings
{
    public string TargetUrl { get; set; } = "https://example.com";
    public int TotalRuns { get; set; } = 5;
    public int Concurrency { get; set; } = 1;
    public bool Headless { get; set; } = true;
    public StepErrorPolicy ErrorPolicy { get; set; } = StepErrorPolicy.SkipStep;
    public bool ScreenshotAfterScenario { get; set; } = true;
    public List<UiScenarioStep> Steps { get; set; } = new();
}

public sealed class UiScenarioStep
{
    public UiStepAction Action { get; set; } = UiStepAction.WaitForVisible;
    public string Selector { get; set; } = string.Empty;
    public string? Text { get; set; }
    public int TimeoutMs { get; set; } = 5000;
}

public enum UiStepAction
{
    WaitForVisible,
    Click,
    FillText
}

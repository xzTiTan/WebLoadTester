using WebLoadTester.Core.Domain;

namespace WebLoadTester.Modules.UiScenario;

public sealed class UiScenarioSettings
{
    public string TargetUrl { get; set; } = "https://example.com";
    public int TotalRuns { get; set; } = 5;
    public int Concurrency { get; set; } = 2;
    public bool Headless { get; set; } = true;
    public StepErrorPolicy StepErrorPolicy { get; set; } = StepErrorPolicy.StopRun;
    public UiScreenshotMode ScreenshotMode { get; set; } = UiScreenshotMode.AfterScenario;
    public List<UiScenarioStep> Steps { get; set; } = new()
    {
        new UiScenarioStep { Action = UiStepAction.WaitForVisible, Selector = "body" }
    };
}

public enum UiStepAction
{
    WaitForVisible,
    Click,
    FillText
}

public enum UiScreenshotMode
{
    AfterScenario
}

public sealed class UiScenarioStep
{
    public UiStepAction Action { get; set; }
    public string Selector { get; set; } = string.Empty;
    public string? Text { get; set; }
    public int TimeoutMs { get; set; } = 5000;
}

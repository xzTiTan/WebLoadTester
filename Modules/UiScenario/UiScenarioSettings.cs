using System.Collections.Generic;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Modules.UiScenario;

/// <summary>
/// Настройки сценарного UI-теста.
/// </summary>
public class UiScenarioSettings
{
    public string TargetUrl { get; set; } = "https://example.com";
    public int TotalRuns { get; set; } = 1;
    public int Concurrency { get; set; } = 1;
    public bool Headless { get; set; } = true;
    public StepErrorPolicy ErrorPolicy { get; set; } = StepErrorPolicy.SkipStep;
    public List<UiStep> Steps { get; set; } = new();
    public bool ScreenshotAfterScenario { get; set; } = true;
}

/// <summary>
/// Описание одного шага UI-сценария.
/// </summary>
public class UiStep
{
    public string Selector { get; set; } = string.Empty;
    public UiStepAction Action { get; set; } = UiStepAction.WaitForVisible;
    public string? Text { get; set; }
    public int TimeoutMs { get; set; } = 5000;
}

/// <summary>
/// Действия шага UI-сценария.
/// </summary>
public enum UiStepAction
{
    WaitForVisible,
    Click,
    FillText
}

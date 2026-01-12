using System.Collections.Generic;
using WebLoadTester.Core.Domain;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;

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
    public int TimeoutMs { get; set; } = 10000;
    public ScreenshotMode ScreenshotMode { get; set; } = ScreenshotMode.OnFailure;
}

/// <summary>
/// Описание одного шага UI-сценария.
/// </summary>
public class UiStep
{
    public string Selector { get; set; } = string.Empty;
    public UiStepAction Action { get; set; } = UiStepAction.WaitForSelector;
    public string? Text { get; set; }
    public int TimeoutMs { get; set; } = 0;
    public int DelayMs { get; set; } = 0;
}

/// <summary>
/// Действия шага UI-сценария.
/// </summary>
public enum UiStepAction
{
    WaitForSelector,
    Click,
    Fill,
    Delay
}

/// <summary>
/// Режим снятия скриншотов.
/// </summary>
public enum ScreenshotMode
{
    Off,
    OnFailure,
    Always
}

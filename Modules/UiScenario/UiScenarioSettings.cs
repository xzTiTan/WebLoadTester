using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
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
public partial class UiStep : ObservableObject
{
    [ObservableProperty]
    private string selector = string.Empty;

    [ObservableProperty]
    private UiStepAction action = UiStepAction.WaitForSelector;

    [ObservableProperty]
    private string? text;

    [ObservableProperty]
    private int timeoutMs;

    [ObservableProperty]
    private int delayMs;

    [ObservableProperty]
    private ObservableCollection<string> clickSelectors = new();

    public void MigrateSelectorToClickSelectors()
    {
        if (!string.IsNullOrWhiteSpace(Selector) && ClickSelectors.Count == 0)
        {
            ClickSelectors.Add(Selector.Trim());
        }
    }

    public IReadOnlyList<string> GetClickSelectors()
    {
        var selectors = ClickSelectors
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (selectors.Count > 0)
        {
            return selectors;
        }

        if (!string.IsNullOrWhiteSpace(Selector))
        {
            return new[] { Selector.Trim() };
        }

        return Array.Empty<string>();
    }

    partial void OnActionChanged(UiStepAction value)
    {
        if (value == UiStepAction.Click)
        {
            MigrateSelectorToClickSelectors();
        }
    }
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

using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Modules.UiScenario;

/// <summary>
/// Настройки сценарного UI-теста.
/// </summary>
public class UiScenarioSettings
{
    public string TargetUrl { get; set; } = "https://www.google.com/";

    /// <summary>
    /// Legacy-поле (больше не используется в MVP, оставлено для обратной совместимости payload).
    /// </summary>
    public StepErrorPolicy ErrorPolicy { get; set; } = StepErrorPolicy.SkipStep;

    public List<UiStep> Steps { get; set; } = new();
    public int TimeoutMs { get; set; } = 10000;
}

/// <summary>
/// Описание одного шага UI-сценария (Variant B).
/// </summary>
public partial class UiStep : ObservableObject
{
    [ObservableProperty]
    private UiStepAction action = UiStepAction.Click;

    [ObservableProperty]
    private string? selector;

    /// <summary>
    /// Основное значение шага (URL/текст/имя скриншота).
    /// </summary>
    [ObservableProperty]
    private string? value;

    /// <summary>
    /// Legacy-поле старого формата Selector+Text. Используется только для миграции при загрузке.
    /// </summary>
    [ObservableProperty]
    private string? text;

    [ObservableProperty]
    private int delayMs;
}

/// <summary>
/// Действия шага UI-сценария (Variant B).
/// Важно: первые значения зафиксированы для обратной совместимости со старыми сериализованными enum-значениями.
/// </summary>
public enum UiStepAction
{
    WaitForSelector = 0,
    Click = 1,
    Fill = 2,
    Delay = 3,
    Navigate = 4,
    AssertText = 5,
    Screenshot = 6
}

using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.UiScenario;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек UI-сценариев.
/// </summary>
public partial class UiScenarioSettingsViewModel : SettingsViewModelBase
{
    private readonly UiScenarioSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public UiScenarioSettingsViewModel(UiScenarioSettings settings)
    {
        _settings = settings;
        Steps = new ObservableCollection<UiStep>(settings.Steps);
        targetUrl = settings.TargetUrl;
        totalRuns = settings.TotalRuns;
        concurrency = settings.Concurrency;
        headless = settings.Headless;
        screenshotAfterScenario = settings.ScreenshotAfterScenario;
        Steps.CollectionChanged += (_, _) => _settings.Steps = Steps.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "UI сценарий";

    public ObservableCollection<UiStep> Steps { get; }

    public UiStepAction[] ActionOptions { get; } = Enum.GetValues<UiStepAction>();

    [ObservableProperty]
    private UiStep? selectedStep;

    [ObservableProperty]
    private string targetUrl = "https://example.com";

    [ObservableProperty]
    private int totalRuns = 1;

    [ObservableProperty]
    private int concurrency = 1;

    [ObservableProperty]
    private bool headless = true;

    [ObservableProperty]
    private bool screenshotAfterScenario = true;

    /// <summary>
    /// Синхронизирует URL сценария.
    /// </summary>
    partial void OnTargetUrlChanged(string value) => _settings.TargetUrl = value;
    /// <summary>
    /// Синхронизирует количество прогонов.
    /// </summary>
    partial void OnTotalRunsChanged(int value) => _settings.TotalRuns = value;
    /// <summary>
    /// Синхронизирует уровень конкурентности.
    /// </summary>
    partial void OnConcurrencyChanged(int value) => _settings.Concurrency = value;
    /// <summary>
    /// Синхронизирует режим headless.
    /// </summary>
    partial void OnHeadlessChanged(bool value) => _settings.Headless = value;
    /// <summary>
    /// Синхронизирует флаг сохранения скриншота после сценария.
    /// </summary>
    partial void OnScreenshotAfterScenarioChanged(bool value) => _settings.ScreenshotAfterScenario = value;

    [RelayCommand]
    private void AddStep()
    {
        var step = new UiStep
        {
            Action = UiStepAction.WaitForVisible,
            Selector = string.Empty,
            Text = string.Empty,
            TimeoutMs = 5000
        };
        Steps.Add(step);
        SelectedStep = step;
    }

    [RelayCommand]
    private void RemoveSelectedStep()
    {
        if (SelectedStep != null)
        {
            Steps.Remove(SelectedStep);
        }
    }

    [RelayCommand]
    private void MoveStepUp()
    {
        if (SelectedStep == null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index > 0)
        {
            Steps.Move(index, index - 1);
        }
    }

    [RelayCommand]
    private void MoveStepDown()
    {
        if (SelectedStep == null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index >= 0 && index < Steps.Count - 1)
        {
            Steps.Move(index, index + 1);
        }
    }
}

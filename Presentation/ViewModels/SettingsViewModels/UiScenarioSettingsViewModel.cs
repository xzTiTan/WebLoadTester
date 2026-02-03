using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Domain;
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
        timeoutMs = settings.TimeoutMs;
        errorPolicy = settings.ErrorPolicy;
        screenshotMode = settings.ScreenshotMode;
        Steps.CollectionChanged += (_, _) => _settings.Steps = Steps.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "UI сценарий";
    public override void UpdateFrom(object settings)
    {
        if (settings is not UiScenarioSettings s)
        {
            return;
        }

        Steps.Clear();
        foreach (var step in s.Steps)
        {
            Steps.Add(step);
        }

        TargetUrl = s.TargetUrl;
        TotalRuns = s.TotalRuns;
        Concurrency = s.Concurrency;
        Headless = s.Headless;
        TimeoutMs = s.TimeoutMs;
        ErrorPolicy = s.ErrorPolicy;
        ScreenshotMode = s.ScreenshotMode;
        _settings.Steps = Steps.ToList();
    }

    public ObservableCollection<UiStep> Steps { get; }

    public StepErrorPolicy[] ErrorPolicyOptions { get; } = Enum.GetValues<StepErrorPolicy>();
    public ScreenshotMode[] ScreenshotModeOptions { get; } = Enum.GetValues<ScreenshotMode>();

    [ObservableProperty]
    private string targetUrl = "https://example.com";

    [ObservableProperty]
    private int totalRuns = 1;

    [ObservableProperty]
    private int concurrency = 1;

    [ObservableProperty]
    private bool headless = true;

    [ObservableProperty]
    private int timeoutMs = 10000;

    [ObservableProperty]
    private StepErrorPolicy errorPolicy;

    [ObservableProperty]
    private ScreenshotMode screenshotMode;

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
    /// Синхронизирует таймаут сценария.
    /// </summary>
    partial void OnTimeoutMsChanged(int value) => _settings.TimeoutMs = value;
    /// <summary>
    /// Синхронизирует политику ошибок шага.
    /// </summary>
    partial void OnErrorPolicyChanged(StepErrorPolicy value) => _settings.ErrorPolicy = value;
    /// <summary>
    /// Синхронизирует режим снятия скриншотов.
    /// </summary>
    partial void OnScreenshotModeChanged(ScreenshotMode value) => _settings.ScreenshotMode = value;

    [RelayCommand]
    private void AddStep()
    {
        var step = new UiStep
        {
            Action = UiStepAction.Click,
            Selector = string.Empty,
            Text = string.Empty,
            TimeoutMs = 0,
            DelayMs = 0
        };
        Steps.Add(step);
    }

    [RelayCommand]
    private void RemoveStep(UiStep? step)
    {
        if (step != null)
        {
            Steps.Remove(step);
        }
    }

    [RelayCommand]
    private void MoveStepUp(UiStep? step)
    {
        if (step == null)
        {
            return;
        }

        var index = Steps.IndexOf(step);
        if (index > 0)
        {
            Steps.Move(index, index - 1);
        }
    }

    [RelayCommand]
    private void MoveStepDown(UiStep? step)
    {
        if (step == null)
        {
            return;
        }

        var index = Steps.IndexOf(step);
        if (index >= 0 && index < Steps.Count - 1)
        {
            Steps.Move(index, index + 1);
        }
    }
}

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
        timeoutMs = settings.TimeoutMs;
        errorPolicy = settings.ErrorPolicy;
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
        TimeoutMs = s.TimeoutMs;
        ErrorPolicy = s.ErrorPolicy;
        _settings.Steps = Steps.ToList();
    }

    public ObservableCollection<UiStep> Steps { get; }

    public StepErrorPolicy[] ErrorPolicyOptions { get; } = Enum.GetValues<StepErrorPolicy>();

    [ObservableProperty]
    private string targetUrl = "https://example.com";

    [ObservableProperty]
    private int timeoutMs = 10000;

    [ObservableProperty]
    private StepErrorPolicy errorPolicy;

    /// <summary>
    /// Синхронизирует URL сценария.
    /// </summary>
    partial void OnTargetUrlChanged(string value) => _settings.TargetUrl = value;

    /// <summary>
    /// Синхронизирует таймаут сценария.
    /// </summary>
    partial void OnTimeoutMsChanged(int value) => _settings.TimeoutMs = value;

    /// <summary>
    /// Синхронизирует политику ошибок шага.
    /// </summary>
    partial void OnErrorPolicyChanged(StepErrorPolicy value) => _settings.ErrorPolicy = value;

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

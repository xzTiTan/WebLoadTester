using System;
using System.Collections.Generic;
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

    public UiScenarioSettingsViewModel(UiScenarioSettings settings)
    {
        _settings = settings;
        NormalizeLegacySteps(_settings);

        Steps = new ObservableCollection<UiStep>(_settings.Steps);
        targetUrl = _settings.TargetUrl;
        timeoutMs = _settings.TimeoutMs;

        Steps.CollectionChanged += (_, _) => SyncSteps();
        foreach (var step in Steps)
        {
            step.PropertyChanged += (_, _) => SyncSteps();
        }
    }

    public override object Settings => _settings;
    public override string Title => "UI сценарий";

    public ObservableCollection<UiStep> Steps { get; }

    public UiStepAction[] ActionOptions { get; } =
    {
        UiStepAction.Navigate,
        UiStepAction.WaitForSelector,
        UiStepAction.Click,
        UiStepAction.Fill,
        UiStepAction.AssertText,
        UiStepAction.Screenshot,
        UiStepAction.Delay
    };

    [ObservableProperty]
    private string targetUrl = string.Empty;

    [ObservableProperty]
    private int timeoutMs = 10000;

    public override void UpdateFrom(object settings)
    {
        if (settings is not UiScenarioSettings incoming)
        {
            return;
        }

        NormalizeLegacySteps(incoming);

        Steps.Clear();
        foreach (var step in incoming.Steps)
        {
            Steps.Add(step);
            step.PropertyChanged += (_, _) => SyncSteps();
        }

        TargetUrl = incoming.TargetUrl;
        TimeoutMs = incoming.TimeoutMs;
        SyncSteps();
    }

    partial void OnTargetUrlChanged(string value) => _settings.TargetUrl = value;

    partial void OnTimeoutMsChanged(int value) => _settings.TimeoutMs = value;

    [RelayCommand]
    private void AddStep()
    {
        var step = new UiStep
        {
            Action = UiStepAction.Click,
            Selector = string.Empty,
            Value = string.Empty,
            DelayMs = 0
        };

        step.PropertyChanged += (_, _) => SyncSteps();
        Steps.Add(step);
        SyncSteps();
    }

    [RelayCommand]
    private void RemoveStep(UiStep? step)
    {
        if (step == null)
        {
            return;
        }

        Steps.Remove(step);
        SyncSteps();
    }


    [RelayCommand]
    private void DuplicateStep(UiStep? step)
    {
        if (step == null)
        {
            return;
        }

        var clone = new UiStep
        {
            Action = step.Action,
            Selector = step.Selector,
            Value = step.Value,
            Text = step.Text,
            DelayMs = step.DelayMs
        };

        clone.PropertyChanged += (_, _) => SyncSteps();
        var index = Steps.IndexOf(step);
        Steps.Insert(index + 1, clone);
        SyncSteps();
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
            SyncSteps();
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
            SyncSteps();
        }
    }

    private void SyncSteps()
    {
        _settings.Steps = Steps.ToList();
    }

    private static void NormalizeLegacySteps(UiScenarioSettings settings)
    {
        if (settings.Steps.Count == 0)
        {
            return;
        }

        foreach (var step in settings.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Value) && !string.IsNullOrWhiteSpace(step.Text))
            {
                step.Value = step.Text;
            }

            if (step.Action == UiStepAction.WaitForSelector)
            {
                var hasSelector = !string.IsNullOrWhiteSpace(step.Selector);
                if (hasSelector)
                {
                    step.Action = string.IsNullOrWhiteSpace(step.Value) ? UiStepAction.Click : UiStepAction.Fill;
                }
            }
        }
    }
}

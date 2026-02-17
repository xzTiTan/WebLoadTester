using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.UiScenario;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

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

    [ObservableProperty] private string targetUrl = string.Empty;
    [ObservableProperty] private int timeoutMs = 10000;
    [ObservableProperty] private UiStep? selectedStep;

    partial void OnSelectedStepChanged(UiStep? value)
    {
        RemoveSelectedStepCommand.NotifyCanExecuteChanged();
        DuplicateSelectedStepCommand.NotifyCanExecuteChanged();
        MoveSelectedStepUpCommand.NotifyCanExecuteChanged();
        MoveSelectedStepDownCommand.NotifyCanExecuteChanged();
    }

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

        SelectedStep = Steps.FirstOrDefault();
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
        SelectedStep = step;
        SyncSteps();
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelectedStep))]
    private void RemoveSelectedStep()
    {
        if (SelectedStep == null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        Steps.Remove(SelectedStep);
        SelectedStep = index >= 0 && Steps.Count > 0
            ? Steps[Math.Min(index, Steps.Count - 1)]
            : null;
        SyncSteps();
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelectedStep))]
    private void DuplicateSelectedStep()
    {
        if (SelectedStep == null)
        {
            return;
        }

        var clone = new UiStep
        {
            Action = SelectedStep.Action,
            Selector = SelectedStep.Selector,
            Value = SelectedStep.Value,
            Text = SelectedStep.Text,
            DelayMs = SelectedStep.DelayMs
        };

        clone.PropertyChanged += (_, _) => SyncSteps();
        var index = Steps.IndexOf(SelectedStep);
        Steps.Insert(index + 1, clone);
        SelectedStep = clone;
        SyncSteps();
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedStepUp))]
    private void MoveSelectedStepUp()
    {
        if (SelectedStep == null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index > 0)
        {
            Steps.Move(index, index - 1);
            SelectedStep = Steps[index - 1];
            SyncSteps();
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedStepDown))]
    private void MoveSelectedStepDown()
    {
        if (SelectedStep == null)
        {
            return;
        }

        var index = Steps.IndexOf(SelectedStep);
        if (index >= 0 && index < Steps.Count - 1)
        {
            Steps.Move(index, index + 1);
            SelectedStep = Steps[index + 1];
            SyncSteps();
        }
    }

    private bool CanMutateSelectedStep() => SelectedStep != null;
    private bool CanMoveSelectedStepUp() => SelectedStep != null && Steps.IndexOf(SelectedStep) > 0;
    private bool CanMoveSelectedStepDown() => SelectedStep != null && Steps.IndexOf(SelectedStep) >= 0 && Steps.IndexOf(SelectedStep) < Steps.Count - 1;

    private void SyncSteps()
    {
        _settings.Steps = Steps.ToList();
        MoveSelectedStepUpCommand.NotifyCanExecuteChanged();
        MoveSelectedStepDownCommand.NotifyCanExecuteChanged();
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

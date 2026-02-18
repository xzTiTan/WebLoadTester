using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.UiScenario;
using WebLoadTester.Presentation.ViewModels.Controls;
using WebLoadTester.Presentation.Common;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels.UiScenario;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class UiScenarioSettingsViewModel : SettingsViewModelBase, IValidatable
{
    private readonly UiScenarioSettings _settings;

    public UiScenarioSettingsViewModel(UiScenarioSettings settings)
    {
        _settings = settings;
        NormalizeLegacySteps(_settings);

        targetUrl = _settings.TargetUrl;
        timeoutMs = _settings.TimeoutMs;

        Steps = new ObservableCollection<UiStep>(_settings.Steps);
        StepRows = new ObservableCollection<UiStepRowViewModel>(Steps.Select(CreateRow));

        StepsEditor = new RowListEditorViewModel();
        StepsEditor.Configure(
            createItem: AddStepInternal,
            removeItem: RemoveStepInternal,
            moveUp: MoveStepUpInternal,
            moveDown: MoveStepDownInternal,
            duplicate: DuplicateStepInternal,
            validationProvider: GetStepValidationErrors,
            selectedItemChanged: item => SelectedStepRow = item as UiStepRowViewModel);

        RefreshEditorItems();
        SelectedStepRow = StepRows.FirstOrDefault();
    }

    public override object Settings => _settings;
    public override string Title => "UI сценарий";

    public ObservableCollection<UiStep> Steps { get; }
    public ObservableCollection<UiStepRowViewModel> StepRows { get; }

    public RowListEditorViewModel StepsEditor { get; }

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
    [ObservableProperty] private UiStepRowViewModel? selectedStepRow;

    public override void UpdateFrom(object settings)
    {
        if (settings is not UiScenarioSettings incoming)
        {
            return;
        }

        NormalizeLegacySteps(incoming);

        Steps.Clear();
        StepRows.Clear();

        foreach (var step in incoming.Steps)
        {
            Steps.Add(step);
            StepRows.Add(CreateRow(step));
        }

        SelectedStepRow = StepRows.FirstOrDefault();
        TargetUrl = incoming.TargetUrl;
        TimeoutMs = incoming.TimeoutMs;
        SyncSteps();
    }

    partial void OnTargetUrlChanged(string value) => _settings.TargetUrl = value;
    partial void OnTimeoutMsChanged(int value) => _settings.TimeoutMs = value;

    private object? AddStepInternal()
    {
        var step = new UiStep
        {
            Action = UiStepAction.Click,
            Selector = string.Empty,
            Value = string.Empty,
            DelayMs = 0
        };

        var row = CreateRow(step);

        if (SelectedStepRow != null)
        {
            var selectedIndex = StepRows.IndexOf(SelectedStepRow);
            if (selectedIndex >= 0)
            {
                var insertIndex = selectedIndex + 1;
                StepRows.Insert(insertIndex, row);
                Steps.Insert(insertIndex, step);
            }
            else
            {
                StepRows.Add(row);
                Steps.Add(step);
            }
        }
        else
        {
            StepRows.Add(row);
            Steps.Add(step);
        }

        SelectedStepRow = row;
        SyncSteps();
        return row;
    }

    private void RemoveStepInternal(object? selected)
    {
        if (selected is not UiStepRowViewModel row)
        {
            return;
        }

        if (StepRows.Count <= 1)
        {
            row.Clear();
            SelectedStepRow = row;
            SyncSteps();
            return;
        }

        var index = StepRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        StepRows.RemoveAt(index);
        Steps.RemoveAt(index);

        if (StepRows.Count > 0)
        {
            SelectedStepRow = StepRows[Math.Min(index, StepRows.Count - 1)];
        }
        else
        {
            SelectedStepRow = null;
        }

        SyncSteps();
    }

    private void DuplicateStepInternal(object? selected)
    {
        if (selected is not UiStepRowViewModel row)
        {
            return;
        }

        var clone = row.Clone();
        clone.NormalizeForAction();

        var index = StepRows.IndexOf(row);
        if (index < 0)
        {
            StepRows.Add(clone);
            Steps.Add(clone.Model);
        }
        else
        {
            StepRows.Insert(index + 1, clone);
            Steps.Insert(index + 1, clone.Model);
        }

        SelectedStepRow = clone;
        SyncSteps();
    }

    private void MoveStepUpInternal(object? selected)
    {
        if (selected is not UiStepRowViewModel row)
        {
            return;
        }

        var index = StepRows.IndexOf(row);
        if (index <= 0)
        {
            return;
        }

        StepRows.Move(index, index - 1);
        Steps.Move(index, index - 1);
        SelectedStepRow = StepRows[index - 1];
        SyncSteps();
    }

    private void MoveStepDownInternal(object? selected)
    {
        if (selected is not UiStepRowViewModel row)
        {
            return;
        }

        var index = StepRows.IndexOf(row);
        if (index < 0 || index >= StepRows.Count - 1)
        {
            return;
        }

        StepRows.Move(index, index + 1);
        Steps.Move(index, index + 1);
        SelectedStepRow = StepRows[index + 1];
        SyncSteps();
    }

    partial void OnSelectedStepRowChanged(UiStepRowViewModel? value)
    {
        StepsEditor.SelectedItem = value;
        StepsEditor.RaiseCommandState();
        StepsEditor.NotifyValidationChanged();
    }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (TimeoutMs < 1000)
        {
            errors.Add("TimeoutMs должен быть >= 1000.");
        }

        errors.AddRange(GetStepValidationErrors());

        return errors
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
    }

    private IEnumerable<string> GetStepValidationErrors()
    {
        return StepRows
            .Select(step => step.RowErrorText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .Cast<string>();
    }

    private UiStepRowViewModel CreateRow(UiStep step)
    {
        var row = new UiStepRowViewModel(step);
        row.PropertyChanged += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(step.Value))
            {
                step.Text = step.Value;
            }

            SyncSteps();
        };

        row.NormalizeForAction();
        return row;
    }

    private void RefreshEditorItems()
    {
        StepsEditor.SetItems(StepRows.Cast<object>());
    }

    private void SyncSteps()
    {
        _settings.Steps = StepRows.Select(r => r.Model).ToList();
        OnPropertyChanged(nameof(Steps));
        StepsEditor.NotifyValidationChanged();
        StepsEditor.RaiseCommandState();
        OnPropertyChanged(nameof(Settings));
        RefreshEditorItems();
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
                step.Value = string.Empty;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Core.Domain;
using WebLoadTester.Modules.UiTiming;
using WebLoadTester.Presentation.ViewModels.Controls;
using WebLoadTester.Presentation.Common;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels.UiTiming;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class UiTimingSettingsViewModel : SettingsViewModelBase, IValidatable
{
    private readonly UiTimingSettings _settings;

    public UiTimingSettingsViewModel(UiTimingSettings settings)
    {
        _settings = settings;
        waitUntil = _settings.WaitUntil;
        timeoutSeconds = _settings.TimeoutSeconds;

        TargetRows = new ObservableCollection<TimingTargetRowViewModel>(_settings.Targets.Select(CreateRow));
        if (TargetRows.Count == 0)
        {
            TargetRows.Add(CreateRow(new TimingTarget { Url = "https://example.com" }));
        }
        TargetsEditor = new RowListEditorViewModel();
        TargetsEditor.Configure(AddTargetInternal, RemoveTargetInternal, MoveTargetUpInternal, MoveTargetDownInternal, DuplicateTargetInternal, GetTargetErrors,
            selectedItemChanged: item => SelectedTargetRow = item as TimingTargetRowViewModel);
        TargetsEditor.SetItems(TargetRows.Cast<object>());
        SelectedTargetRow = TargetRows.FirstOrDefault();
        SyncTargets();
    }

    public override object Settings => _settings;
    public override string Title => "UI тайминги";

    public ObservableCollection<TimingTargetRowViewModel> TargetRows { get; }
    public RowListEditorViewModel TargetsEditor { get; }
    public Array WaitUntilOptions { get; } = Enum.GetValues(typeof(UiWaitUntil));

    [ObservableProperty] private TimingTargetRowViewModel? selectedTargetRow;
    [ObservableProperty] private UiWaitUntil waitUntil = UiWaitUntil.DomContentLoaded;
    [ObservableProperty] private int timeoutSeconds = 30;

    partial void OnSelectedTargetRowChanged(TimingTargetRowViewModel? value)
    {
        TargetsEditor.SelectedItem = value;
        TargetsEditor.RaiseCommandState();
        TargetsEditor.NotifyValidationChanged();
    }

    public override void UpdateFrom(object settings)
    {
        if (settings is not UiTimingSettings s)
        {
            return;
        }

        TargetRows.Clear();
        foreach (var target in s.Targets)
        {
            TargetRows.Add(CreateRow(target));
        }

        if (TargetRows.Count == 0)
        {
            TargetRows.Add(CreateRow(new TimingTarget { Url = "https://example.com" }));
        }

        TargetsEditor.SetItems(TargetRows.Cast<object>());
        SelectedTargetRow = TargetRows.FirstOrDefault();

        WaitUntil = s.WaitUntil;
        TimeoutSeconds = s.TimeoutSeconds;
        SyncTargets();
    }

    partial void OnWaitUntilChanged(UiWaitUntil value) => _settings.WaitUntil = value;
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

    private object? AddTargetInternal()
    {
        var row = CreateRow(new TimingTarget { Url = "https://example.com" });
        var insertIndex = SelectedTargetRow != null ? TargetRows.IndexOf(SelectedTargetRow) + 1 : TargetRows.Count;
        if (insertIndex < 0 || insertIndex > TargetRows.Count)
        {
            insertIndex = TargetRows.Count;
        }

        TargetRows.Insert(insertIndex, row);
        SelectedTargetRow = row;
        SyncTargets();
        return row;
    }

    private void RemoveTargetInternal(object? selected)
    {
        if (selected is not TimingTargetRowViewModel row)
        {
            return;
        }

        if (TargetRows.Count <= 1)
        {
            row.Clear();
            SyncTargets();
            return;
        }

        var index = TargetRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        TargetRows.RemoveAt(index);
        SelectedTargetRow = TargetRows.Count > 0 ? TargetRows[Math.Min(index, TargetRows.Count - 1)] : null;
        SyncTargets();
    }

    private void DuplicateTargetInternal(object? selected)
    {
        if (selected is not TimingTargetRowViewModel row)
        {
            return;
        }

        var clone = row.Clone();
        var index = TargetRows.IndexOf(row);
        TargetRows.Insert(index + 1, clone);
        SelectedTargetRow = clone;
        SyncTargets();
    }

    private void MoveTargetUpInternal(object? selected)
    {
        if (selected is not TimingTargetRowViewModel row)
        {
            return;
        }

        var index = TargetRows.IndexOf(row);
        if (index > 0)
        {
            TargetRows.Move(index, index - 1);
            SelectedTargetRow = TargetRows[index - 1];
            SyncTargets();
        }
    }

    private void MoveTargetDownInternal(object? selected)
    {
        if (selected is not TimingTargetRowViewModel row)
        {
            return;
        }

        var index = TargetRows.IndexOf(row);
        if (index >= 0 && index < TargetRows.Count - 1)
        {
            TargetRows.Move(index, index + 1);
            SelectedTargetRow = TargetRows[index + 1];
            SyncTargets();
        }
    }


    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (TimeoutSeconds < 1)
        {
            errors.Add("Таймаут в секундах должен быть >= 1.");
        }

        errors.AddRange(GetTargetErrors());

        return errors
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
    }

    private IEnumerable<string> GetTargetErrors() => TargetRows.Select(r => r.RowErrorText).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct();

    private TimingTargetRowViewModel CreateRow(TimingTarget target)
    {
        var row = new TimingTargetRowViewModel(target);
        row.PropertyChanged += (_, _) => SyncTargets();
        return row;
    }

    private void SyncTargets()
    {
        _settings.Targets = TargetRows.Select(r => r.Model).ToList();
        TargetsEditor.SetItems(TargetRows.Cast<object>());
        TargetsEditor.NotifyValidationChanged();
        TargetsEditor.RaiseCommandState();
        OnPropertyChanged(nameof(Settings));
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Core.Domain;
using WebLoadTester.Modules.UiSnapshot;
using WebLoadTester.Presentation.ViewModels.Controls;
using WebLoadTester.Presentation.Common;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels.UiSnapshot;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class UiSnapshotSettingsViewModel : SettingsViewModelBase, IValidatable
{
    private readonly UiSnapshotSettings _settings;

    public UiSnapshotSettingsViewModel(UiSnapshotSettings settings)
    {
        _settings = settings;
        NormalizeLegacyTargets(_settings);

        waitUntil = _settings.WaitUntil;
        timeoutSeconds = _settings.TimeoutSeconds;
        viewportWidth = _settings.ViewportWidth;
        viewportHeight = _settings.ViewportHeight;
        fullPage = _settings.FullPage;

        TargetRows = new ObservableCollection<SnapshotTargetRowViewModel>(_settings.Targets.Select(CreateRow));
        if (TargetRows.Count == 0)
        {
            TargetRows.Add(CreateRow(new SnapshotTarget { Url = "https://example.com", Name = "example" }));
        }
        TargetsEditor = new RowListEditorViewModel();
        TargetsEditor.Configure(AddTargetInternal, RemoveTargetInternal, MoveTargetUpInternal, MoveTargetDownInternal, DuplicateTargetInternal, GetTargetErrors,
            selectedItemChanged: item => SelectedTargetRow = item as SnapshotTargetRowViewModel);
        TargetsEditor.SetItems(TargetRows.Cast<object>());
        SelectedTargetRow = TargetRows.FirstOrDefault();
    }

    public override object Settings => _settings;
    public override string Title => "UI снимки";

    public ObservableCollection<SnapshotTargetRowViewModel> TargetRows { get; }
    public RowListEditorViewModel TargetsEditor { get; }
    public Array WaitUntilOptions { get; } = Enum.GetValues(typeof(UiWaitUntil));

    [ObservableProperty] private SnapshotTargetRowViewModel? selectedTargetRow;
    [ObservableProperty] private UiWaitUntil waitUntil = UiWaitUntil.DomContentLoaded;
    [ObservableProperty] private int timeoutSeconds = 30;
    [ObservableProperty] private int? viewportWidth;
    [ObservableProperty] private int? viewportHeight;
    [ObservableProperty] private bool fullPage = true;

    partial void OnSelectedTargetRowChanged(SnapshotTargetRowViewModel? value)
    {
        TargetsEditor.SelectedItem = value;
        TargetsEditor.RaiseCommandState();
        TargetsEditor.NotifyValidationChanged();
    }

    public override void UpdateFrom(object settings)
    {
        if (settings is not UiSnapshotSettings s)
        {
            return;
        }

        NormalizeLegacyTargets(s);
        TargetRows.Clear();
        foreach (var target in s.Targets)
        {
            TargetRows.Add(CreateRow(target));
        }

        if (TargetRows.Count == 0)
        {
            TargetRows.Add(CreateRow(new SnapshotTarget { Url = "https://example.com", Name = "example" }));
        }

        TargetsEditor.SetItems(TargetRows.Cast<object>());
        SelectedTargetRow = TargetRows.FirstOrDefault();

        WaitUntil = s.WaitUntil;
        TimeoutSeconds = s.TimeoutSeconds;
        ViewportWidth = s.ViewportWidth;
        ViewportHeight = s.ViewportHeight;
        FullPage = s.FullPage;
        _settings.ScreenshotFormat = "png";
        SyncTargets();
    }

    partial void OnWaitUntilChanged(UiWaitUntil value) => _settings.WaitUntil = value;
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;
    partial void OnViewportWidthChanged(int? value) => _settings.ViewportWidth = value;
    partial void OnViewportHeightChanged(int? value) => _settings.ViewportHeight = value;
    partial void OnFullPageChanged(bool value)
    {
        _settings.FullPage = value;
        foreach (var row in TargetRows)
        {
            row.RefreshComputed();
        }
    }

    private object? AddTargetInternal()
    {
        var row = CreateRow(new SnapshotTarget { Url = "https://example.com", Name = "example" });
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
        if (selected is not SnapshotTargetRowViewModel row)
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
        if (selected is not SnapshotTargetRowViewModel row)
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
        if (selected is not SnapshotTargetRowViewModel row)
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
        if (selected is not SnapshotTargetRowViewModel row)
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
            errors.Add("TimeoutSeconds должен быть >= 1.");
        }

        errors.AddRange(GetTargetErrors());

        return errors
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
    }

    private IEnumerable<string> GetTargetErrors() => TargetRows.Select(r => r.RowErrorText).Where(e => !string.IsNullOrWhiteSpace(e)).Distinct();

    private SnapshotTargetRowViewModel CreateRow(SnapshotTarget target)
    {
        var row = new SnapshotTargetRowViewModel(target);
        row.PropertyChanged += (_, _) => SyncTargets();
        return row;
    }

    private void SyncTargets()
    {
        _settings.Targets = TargetRows.Select(r => r.Model).ToList();
        _settings.ScreenshotFormat = "png";
        TargetsEditor.SetItems(TargetRows.Cast<object>());
        TargetsEditor.NotifyValidationChanged();
        TargetsEditor.RaiseCommandState();
        OnPropertyChanged(nameof(Settings));
    }

    private static void NormalizeLegacyTargets(UiSnapshotSettings settings)
    {
        foreach (var target in settings.Targets)
        {
            if (string.IsNullOrWhiteSpace(target.Name) && !string.IsNullOrWhiteSpace(target.Tag))
            {
                target.Name = target.Tag;
            }
        }
    }
}

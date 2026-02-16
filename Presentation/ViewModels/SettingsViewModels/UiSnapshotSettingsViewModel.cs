using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Domain;
using WebLoadTester.Modules.UiSnapshot;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек UI-снимков.
/// </summary>
public partial class UiSnapshotSettingsViewModel : SettingsViewModelBase
{
    private readonly UiSnapshotSettings _settings;

    public UiSnapshotSettingsViewModel(UiSnapshotSettings settings)
    {
        _settings = settings;
        NormalizeLegacyTargets(_settings);

        Targets = new ObservableCollection<SnapshotTarget>(_settings.Targets);
        waitUntil = _settings.WaitUntil;
        timeoutSeconds = _settings.TimeoutSeconds;
        viewportWidth = _settings.ViewportWidth;
        viewportHeight = _settings.ViewportHeight;
        fullPage = _settings.FullPage;

        Targets.CollectionChanged += (_, _) => SyncTargets();
    }

    public override object Settings => _settings;
    public override string Title => "UI снимки";

    public ObservableCollection<SnapshotTarget> Targets { get; }
    public Array WaitUntilOptions { get; } = Enum.GetValues(typeof(UiWaitUntil));

    [ObservableProperty]
    private SnapshotTarget? selectedTarget;

    [ObservableProperty]
    private UiWaitUntil waitUntil = UiWaitUntil.DomContentLoaded;

    [ObservableProperty]
    private int timeoutSeconds = 30;

    [ObservableProperty]
    private int? viewportWidth;

    [ObservableProperty]
    private int? viewportHeight;

    [ObservableProperty]
    private bool fullPage = true;

    public override void UpdateFrom(object settings)
    {
        if (settings is not UiSnapshotSettings s)
        {
            return;
        }

        NormalizeLegacyTargets(s);

        Targets.Clear();
        foreach (var target in s.Targets)
        {
            Targets.Add(target);
        }

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
    partial void OnFullPageChanged(bool value) => _settings.FullPage = value;

    [RelayCommand]
    private void AddTarget()
    {
        var target = new SnapshotTarget { Url = "https://example.com", Name = "example" };
        Targets.Add(target);
        SelectedTarget = target;
        SyncTargets();
    }

    [RelayCommand]
    private void RemoveSelectedTarget()
    {
        if (SelectedTarget == null)
        {
            return;
        }

        Targets.Remove(SelectedTarget);
        SyncTargets();
    }


    [RelayCommand]
    private void DuplicateSelectedTarget()
    {
        if (SelectedTarget == null)
        {
            return;
        }

        var clone = new SnapshotTarget
        {
            Url = SelectedTarget.Url,
            Selector = SelectedTarget.Selector,
            Name = SelectedTarget.Name,
            Tag = SelectedTarget.Tag
        };

        var index = Targets.IndexOf(SelectedTarget);
        Targets.Insert(index + 1, clone);
        SelectedTarget = clone;
        SyncTargets();
    }

    [RelayCommand]
    private void MoveTargetUp()
    {
        if (SelectedTarget == null)
        {
            return;
        }

        var index = Targets.IndexOf(SelectedTarget);
        if (index > 0)
        {
            Targets.Move(index, index - 1);
            SyncTargets();
        }
    }

    [RelayCommand]
    private void MoveTargetDown()
    {
        if (SelectedTarget == null)
        {
            return;
        }

        var index = Targets.IndexOf(SelectedTarget);
        if (index >= 0 && index < Targets.Count - 1)
        {
            Targets.Move(index, index + 1);
            SyncTargets();
        }
    }

    private void SyncTargets()
    {
        _settings.Targets = Targets.ToList();
        _settings.ScreenshotFormat = "png";
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

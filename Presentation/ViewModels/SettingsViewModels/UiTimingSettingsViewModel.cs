using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Domain;
using WebLoadTester.Modules.UiTiming;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек UI-таймингов.
/// </summary>
public partial class UiTimingSettingsViewModel : SettingsViewModelBase
{
    private readonly UiTimingSettings _settings;

    public UiTimingSettingsViewModel(UiTimingSettings settings)
    {
        _settings = settings;
        Targets = new ObservableCollection<TimingTarget>(_settings.Targets);
        waitUntil = _settings.WaitUntil;
        timeoutSeconds = _settings.TimeoutSeconds;
        Targets.CollectionChanged += (_, _) => SyncTargets();
    }

    public override object Settings => _settings;
    public override string Title => "UI тайминги";

    public ObservableCollection<TimingTarget> Targets { get; }
    public Array WaitUntilOptions { get; } = Enum.GetValues(typeof(UiWaitUntil));

    [ObservableProperty]
    private TimingTarget? selectedTarget;

    [ObservableProperty]
    private UiWaitUntil waitUntil = UiWaitUntil.DomContentLoaded;

    [ObservableProperty]
    private int timeoutSeconds = 30;

    public override void UpdateFrom(object settings)
    {
        if (settings is not UiTimingSettings s)
        {
            return;
        }

        Targets.Clear();
        foreach (var target in s.Targets)
        {
            Targets.Add(target);
        }

        WaitUntil = s.WaitUntil;
        TimeoutSeconds = s.TimeoutSeconds;
        SyncTargets();
    }

    partial void OnWaitUntilChanged(UiWaitUntil value) => _settings.WaitUntil = value;
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

    [RelayCommand]
    private void AddTarget()
    {
        var target = new TimingTarget { Url = "https://example.com" };
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
    }
}

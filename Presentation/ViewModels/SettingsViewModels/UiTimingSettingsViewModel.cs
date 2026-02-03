using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.UiTiming;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек UI-таймингов.
/// </summary>
public partial class UiTimingSettingsViewModel : SettingsViewModelBase
{
    private readonly UiTimingSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public UiTimingSettingsViewModel(UiTimingSettings settings)
    {
        _settings = settings;
        Targets = new ObservableCollection<TimingTarget>(settings.Targets);
        waitUntil = settings.WaitUntil;
        timeoutSeconds = settings.TimeoutSeconds;
        Targets.CollectionChanged += (_, _) => _settings.Targets = Targets.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "UI тайминги";
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
        _settings.Targets = Targets.ToList();
    }

    public ObservableCollection<TimingTarget> Targets { get; }

    public string[] WaitUntilOptions { get; } = { "load", "domcontentloaded", "networkidle" };

    [ObservableProperty]
    private TimingTarget? selectedTarget;

    [ObservableProperty]
    private string waitUntil = "load";

    [ObservableProperty]
    private int timeoutSeconds = 30;

    /// <summary>
    /// Синхронизирует режим ожидания загрузки.
    /// </summary>
    partial void OnWaitUntilChanged(string value) => _settings.WaitUntil = value;
    /// <summary>
    /// Синхронизирует таймаут навигации.
    /// </summary>
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

    [RelayCommand]
    private void AddTarget()
    {
        var target = new TimingTarget { Url = "https://example.com" };
        Targets.Add(target);
        SelectedTarget = target;
    }

    [RelayCommand]
    private void RemoveSelectedTarget()
    {
        if (SelectedTarget != null)
        {
            Targets.Remove(SelectedTarget);
        }
    }

    [RelayCommand]
    private void ClearTargets()
    {
        Targets.Clear();
    }
}

using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.UiSnapshot;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек UI-снимков.
/// </summary>
public partial class UiSnapshotSettingsViewModel : SettingsViewModelBase
{
    private readonly UiSnapshotSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public UiSnapshotSettingsViewModel(UiSnapshotSettings settings)
    {
        _settings = settings;
        Targets = new ObservableCollection<SnapshotTarget>(settings.Targets);
        concurrency = settings.Concurrency;
        repeatsPerUrl = settings.RepeatsPerUrl;
        waitUntil = settings.WaitUntil;
        extraDelayMs = settings.ExtraDelayMs;
        fullPage = settings.FullPage;
        Targets.CollectionChanged += (_, _) => _settings.Targets = Targets.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "UI снимки";

    public ObservableCollection<SnapshotTarget> Targets { get; }

    public string[] WaitUntilOptions { get; } = { "load", "domcontentloaded", "networkidle" };

    [ObservableProperty]
    private SnapshotTarget? selectedTarget;

    [ObservableProperty]
    private int concurrency;

    [ObservableProperty]
    private int repeatsPerUrl = 1;

    [ObservableProperty]
    private string waitUntil = "load";

    [ObservableProperty]
    private int extraDelayMs;

    [ObservableProperty]
    private bool fullPage = true;

    /// <summary>
    /// Синхронизирует уровень конкурентности.
    /// </summary>
    partial void OnConcurrencyChanged(int value) => _settings.Concurrency = value;
    /// <summary>
    /// Синхронизирует количество повторов на URL.
    /// </summary>
    partial void OnRepeatsPerUrlChanged(int value) => _settings.RepeatsPerUrl = value;
    /// <summary>
    /// Синхронизирует режим ожидания загрузки.
    /// </summary>
    partial void OnWaitUntilChanged(string value) => _settings.WaitUntil = value;
    /// <summary>
    /// Синхронизирует дополнительную задержку.
    /// </summary>
    partial void OnExtraDelayMsChanged(int value) => _settings.ExtraDelayMs = value;
    /// <summary>
    /// Синхронизирует флаг полного снимка страницы.
    /// </summary>
    partial void OnFullPageChanged(bool value) => _settings.FullPage = value;

    [RelayCommand]
    private void AddTarget()
    {
        var target = new SnapshotTarget { Url = "https://example.com" };
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

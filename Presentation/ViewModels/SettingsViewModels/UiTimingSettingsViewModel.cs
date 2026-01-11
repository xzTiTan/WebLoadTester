using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
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
        Urls = new ObservableCollection<string>(settings.Urls);
        repeatsPerUrl = settings.RepeatsPerUrl;
        concurrency = settings.Concurrency;
        waitUntil = settings.WaitUntil;
        Urls.CollectionChanged += (_, _) => _settings.Urls = Urls.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "UI Timing";

    public ObservableCollection<string> Urls { get; }

    [ObservableProperty]
    private int repeatsPerUrl;

    [ObservableProperty]
    private int concurrency;

    [ObservableProperty]
    private string waitUntil = "load";

    /// <summary>
    /// Синхронизирует количество повторов на URL.
    /// </summary>
    partial void OnRepeatsPerUrlChanged(int value) => _settings.RepeatsPerUrl = value;
    /// <summary>
    /// Синхронизирует уровень конкурентности.
    /// </summary>
    partial void OnConcurrencyChanged(int value) => _settings.Concurrency = value;
    /// <summary>
    /// Синхронизирует режим ожидания загрузки.
    /// </summary>
    partial void OnWaitUntilChanged(string value) => _settings.WaitUntil = value;
}

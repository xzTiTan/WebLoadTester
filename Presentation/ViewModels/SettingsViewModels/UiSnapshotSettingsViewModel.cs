using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
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
        Urls = new ObservableCollection<string>(settings.Urls);
        concurrency = settings.Concurrency;
        waitMode = settings.WaitMode;
        delayAfterLoadMs = settings.DelayAfterLoadMs;
        Urls.CollectionChanged += (_, _) => _settings.Urls = Urls.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "UI снимки";

    public ObservableCollection<string> Urls { get; }

    [ObservableProperty]
    private int concurrency;

    [ObservableProperty]
    private string waitMode = "load";

    [ObservableProperty]
    private int delayAfterLoadMs;

    /// <summary>
    /// Синхронизирует уровень конкурентности.
    /// </summary>
    partial void OnConcurrencyChanged(int value) => _settings.Concurrency = value;
    /// <summary>
    /// Синхронизирует режим ожидания загрузки.
    /// </summary>
    partial void OnWaitModeChanged(string value) => _settings.WaitMode = value;
    /// <summary>
    /// Синхронизирует задержку после загрузки.
    /// </summary>
    partial void OnDelayAfterLoadMsChanged(int value) => _settings.DelayAfterLoadMs = value;
}

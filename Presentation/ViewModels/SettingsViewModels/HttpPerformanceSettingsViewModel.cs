using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpPerformance;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек нагрузочного HTTP-теста.
/// </summary>
public partial class HttpPerformanceSettingsViewModel : SettingsViewModelBase
{
    private readonly HttpPerformanceSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public HttpPerformanceSettingsViewModel(HttpPerformanceSettings settings)
    {
        _settings = settings;
        url = settings.Url;
        totalRequests = settings.TotalRequests;
        concurrency = settings.Concurrency;
        rpsLimit = settings.RpsLimit ?? 0;
        timeoutSeconds = settings.TimeoutSeconds;
    }

    public override object Settings => _settings;
    public override string Title => "HTTP производительность";
    public override void UpdateFrom(object settings)
    {
        if (settings is not HttpPerformanceSettings s)
        {
            return;
        }

        Url = s.Url;
        TotalRequests = s.TotalRequests;
        Concurrency = s.Concurrency;
        RpsLimit = s.RpsLimit ?? 0;
        TimeoutSeconds = s.TimeoutSeconds;
    }

    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    private int totalRequests;

    [ObservableProperty]
    private int concurrency;

    [ObservableProperty]
    private int rpsLimit;

    [ObservableProperty]
    private int timeoutSeconds;

    /// <summary>
    /// Синхронизирует URL запроса.
    /// </summary>
    partial void OnUrlChanged(string value) => _settings.Url = value;
    /// <summary>
    /// Синхронизирует количество запросов.
    /// </summary>
    partial void OnTotalRequestsChanged(int value) => _settings.TotalRequests = value;
    /// <summary>
    /// Синхронизирует уровень конкурентности.
    /// </summary>
    partial void OnConcurrencyChanged(int value) => _settings.Concurrency = value;
    /// <summary>
    /// Синхронизирует ограничение RPS (0 отключает).
    /// </summary>
    partial void OnRpsLimitChanged(int value) => _settings.RpsLimit = value > 0 ? value : null;
    /// <summary>
    /// Синхронизирует таймаут запросов.
    /// </summary>
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;
}

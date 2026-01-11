using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpPerformance;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class HttpPerformanceSettingsViewModel : SettingsViewModelBase
{
    private readonly HttpPerformanceSettings _settings;

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
    public override string Title => "HTTP Performance";

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

    partial void OnUrlChanged(string value) => _settings.Url = value;
    partial void OnTotalRequestsChanged(int value) => _settings.TotalRequests = value;
    partial void OnConcurrencyChanged(int value) => _settings.Concurrency = value;
    partial void OnRpsLimitChanged(int value) => _settings.RpsLimit = value > 0 ? value : null;
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;
}

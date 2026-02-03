using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        baseUrl = settings.BaseUrl;
        Endpoints = new ObservableCollection<HttpPerformanceEndpoint>(settings.Endpoints);
        timeoutSeconds = settings.TimeoutSeconds;
        Endpoints.CollectionChanged += (_, _) => _settings.Endpoints = Endpoints.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "HTTP производительность";
    public override void UpdateFrom(object settings)
    {
        if (settings is not HttpPerformanceSettings s)
        {
            return;
        }

        BaseUrl = s.BaseUrl;
        Endpoints.Clear();
        foreach (var endpoint in s.Endpoints)
        {
            Endpoints.Add(endpoint);
        }
        TimeoutSeconds = s.TimeoutSeconds;
        _settings.Endpoints = Endpoints.ToList();
    }

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private int timeoutSeconds;

    public ObservableCollection<HttpPerformanceEndpoint> Endpoints { get; }

    public string[] MethodOptions { get; } = { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };

    [ObservableProperty]
    private HttpPerformanceEndpoint? selectedEndpoint;

    /// <summary>
    /// Синхронизирует URL запроса.
    /// </summary>
    partial void OnBaseUrlChanged(string value) => _settings.BaseUrl = value;
    /// <summary>
    /// Синхронизирует таймаут запросов.
    /// </summary>
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

    [RelayCommand]
    private void AddEndpoint()
    {
        var endpoint = new HttpPerformanceEndpoint { Name = "Endpoint", Method = "GET", Path = "/" };
        Endpoints.Add(endpoint);
        SelectedEndpoint = endpoint;
    }

    [RelayCommand]
    private void RemoveSelectedEndpoint()
    {
        if (SelectedEndpoint != null)
        {
            Endpoints.Remove(SelectedEndpoint);
        }
    }
}

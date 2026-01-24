using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.HttpFunctional;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек функциональных HTTP-проверок.
/// </summary>
public partial class HttpFunctionalSettingsViewModel : SettingsViewModelBase
{
    private readonly HttpFunctionalSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public HttpFunctionalSettingsViewModel(HttpFunctionalSettings settings)
    {
        _settings = settings;
        baseUrl = settings.BaseUrl;
        Endpoints = new ObservableCollection<HttpEndpoint>(settings.Endpoints);
        timeoutSeconds = settings.TimeoutSeconds;
        Endpoints.CollectionChanged += (_, _) => _settings.Endpoints = Endpoints.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "HTTP функциональные проверки";
    public override void UpdateFrom(object settings)
    {
        if (settings is not HttpFunctionalSettings s)
        {
            return;
        }

        BaseUrl = s.BaseUrl;
        TimeoutSeconds = s.TimeoutSeconds;
        Endpoints.Clear();
        foreach (var endpoint in s.Endpoints)
        {
            Endpoints.Add(endpoint);
        }
        _settings.Endpoints = Endpoints.ToList();
    }

    public ObservableCollection<HttpEndpoint> Endpoints { get; }

    public string[] MethodOptions { get; } = { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private HttpEndpoint? selectedEndpoint;

    [ObservableProperty]
    private int timeoutSeconds;

    /// <summary>
    /// Синхронизирует базовый URL.
    /// </summary>
    partial void OnBaseUrlChanged(string value) => _settings.BaseUrl = value;
    /// <summary>
    /// Синхронизирует таймаут запросов.
    /// </summary>
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

    [RelayCommand]
    private void AddEndpoint()
    {
        var endpoint = new HttpEndpoint
        {
            Name = "New endpoint",
            Method = "GET",
            Path = "/"
        };
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

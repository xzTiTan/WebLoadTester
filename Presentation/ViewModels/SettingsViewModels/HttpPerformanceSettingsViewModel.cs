using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.HttpPerformance;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек HTTP-производительности.
/// </summary>
public partial class HttpPerformanceSettingsViewModel : SettingsViewModelBase
{
    private readonly HttpPerformanceSettings _settings;

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
        TimeoutSeconds = s.TimeoutSeconds;
        Endpoints.Clear();
        foreach (var endpoint in s.Endpoints)
        {
            Endpoints.Add(endpoint);
        }

        _settings.Endpoints = Endpoints.ToList();
    }

    public ObservableCollection<HttpPerformanceEndpoint> Endpoints { get; }

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private int timeoutSeconds;

    [ObservableProperty]
    private HttpPerformanceEndpoint? selectedEndpoint;

    partial void OnBaseUrlChanged(string value) => _settings.BaseUrl = value;
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

    [RelayCommand]
    private void AddEndpoint()
    {
        var endpoint = new HttpPerformanceEndpoint
        {
            Name = "Endpoint",
            Method = "GET",
            Path = "/"
        };

        Endpoints.Add(endpoint);
        SelectedEndpoint = endpoint;
    }

    [RelayCommand]
    private void DuplicateSelectedEndpoint()
    {
        if (SelectedEndpoint == null)
        {
            return;
        }

        var copy = new HttpPerformanceEndpoint
        {
            Name = $"{SelectedEndpoint.Name} Copy",
            Method = SelectedEndpoint.Method,
            Path = SelectedEndpoint.Path,
            ExpectedStatusCode = SelectedEndpoint.ExpectedStatusCode
        };

        Endpoints.Add(copy);
        SelectedEndpoint = copy;
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

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

    public HttpFunctionalSettingsViewModel(HttpFunctionalSettings settings)
    {
        _settings = settings;
        foreach (var endpoint in settings.Endpoints)
        {
            endpoint.NormalizeLegacy();
        }

        baseUrl = settings.BaseUrl;
        Endpoints = new ObservableCollection<HttpFunctionalEndpoint>(settings.Endpoints);
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

        foreach (var endpoint in s.Endpoints)
        {
            endpoint.NormalizeLegacy();
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

    public ObservableCollection<HttpFunctionalEndpoint> Endpoints { get; }

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private HttpFunctionalEndpoint? selectedEndpoint;

    partial void OnSelectedEndpointChanged(HttpFunctionalEndpoint? value)
    {
        RemoveSelectedEndpointCommand.NotifyCanExecuteChanged();
        DuplicateSelectedEndpointCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private int timeoutSeconds;

    partial void OnBaseUrlChanged(string value) => _settings.BaseUrl = value;
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

    [RelayCommand]
    private void AddEndpoint()
    {
        var endpoint = new HttpFunctionalEndpoint
        {
            Name = "Endpoint",
            Method = "GET",
            Path = "/",
            ExpectedStatusCode = 200
        };
        Endpoints.Add(endpoint);
        SelectedEndpoint = endpoint;
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelectedEndpoint))]
    private void DuplicateSelectedEndpoint()
    {
        if (SelectedEndpoint == null)
        {
            return;
        }

        var copy = new HttpFunctionalEndpoint
        {
            Name = $"{SelectedEndpoint.Name} Copy",
            Method = SelectedEndpoint.Method,
            Path = SelectedEndpoint.Path,
            ExpectedStatusCode = SelectedEndpoint.ExpectedStatusCode,
            BodyContains = SelectedEndpoint.BodyContains,
            RequiredHeaders = SelectedEndpoint.RequiredHeaders.ToList(),
            JsonFieldEquals = SelectedEndpoint.JsonFieldEquals.ToList()
        };

        var index = Endpoints.IndexOf(SelectedEndpoint);
        Endpoints.Insert(index + 1, copy);
        SelectedEndpoint = copy;
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelectedEndpoint))]
    private void RemoveSelectedEndpoint()
    {
        if (SelectedEndpoint != null)
        {
            Endpoints.Remove(SelectedEndpoint);
        }
    }

    private bool CanMutateSelectedEndpoint() => SelectedEndpoint != null;
}

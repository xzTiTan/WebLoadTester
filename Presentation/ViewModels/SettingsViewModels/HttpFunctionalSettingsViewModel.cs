using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpFunctional;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class HttpFunctionalSettingsViewModel : ObservableObject, ISettingsViewModel
{
    public ObservableCollection<HttpEndpointViewModel> Endpoints { get; } = new()
    {
        new HttpEndpointViewModel { Name = "Example", Url = "https://example.com", Method = "GET" }
    };

    [ObservableProperty]
    private int timeoutSeconds = 20;

    public object BuildSettings()
    {
        return new HttpFunctionalSettings
        {
            TimeoutSeconds = TimeoutSeconds,
            Endpoints = Endpoints.Select(e => e.ToEndpoint()).ToList()
        };
    }
}

public partial class HttpEndpointViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    private string method = "GET";

    [ObservableProperty]
    private int? statusCodeEquals = 200;

    [ObservableProperty]
    private int? maxLatencyMs = 2000;

    public HttpEndpoint ToEndpoint()
    {
        return new HttpEndpoint
        {
            Name = Name,
            Url = Url,
            Method = Method,
            Asserts = new HttpAssertSettings
            {
                StatusCodeEquals = StatusCodeEquals,
                MaxLatencyMs = MaxLatencyMs
            }
        };
    }
}

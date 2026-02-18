using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpPerformance;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels.HttpFunctional;

public partial class HttpPerformanceEndpointRowViewModel : ObservableObject
{
    public HttpPerformanceEndpointRowViewModel(HttpPerformanceEndpoint model)
    {
        Model = model;
        method = model.Method;
        url = model.Path;
    }

    public HttpPerformanceEndpoint Model { get; }

    public string[] Methods { get; } = { "GET", "POST", "PUT", "DELETE", "PATCH" };

    [ObservableProperty] private string method = "GET";
    [ObservableProperty] private string url = "/";

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);
    public string RowErrorText => string.IsNullOrWhiteSpace(Url) ? "Endpoint: Url обязателен" : string.Empty;

    partial void OnMethodChanged(string value)
    {
        Model.Method = string.IsNullOrWhiteSpace(value) ? "GET" : value;
    }

    partial void OnUrlChanged(string value)
    {
        Model.Path = value;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    public HttpPerformanceEndpointRowViewModel Clone() => new(new HttpPerformanceEndpoint
    {
        Name = Model.Name,
        Method = Method,
        Path = Url,
        ExpectedStatusCode = Model.ExpectedStatusCode
    });

    public void Clear()
    {
        Method = "GET";
        Url = string.Empty;
    }
}

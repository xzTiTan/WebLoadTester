using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpFunctional;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class HttpFunctionalSettingsViewModel : SettingsViewModelBase
{
    private readonly HttpFunctionalSettings _settings;

    public HttpFunctionalSettingsViewModel(HttpFunctionalSettings settings)
    {
        _settings = settings;
        Endpoints = new ObservableCollection<HttpEndpoint>(settings.Endpoints);
        timeoutSeconds = settings.TimeoutSeconds;
        Endpoints.CollectionChanged += (_, _) => _settings.Endpoints = Endpoints.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "HTTP Functional";

    public ObservableCollection<HttpEndpoint> Endpoints { get; }

    [ObservableProperty]
    private int timeoutSeconds;

    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;
}

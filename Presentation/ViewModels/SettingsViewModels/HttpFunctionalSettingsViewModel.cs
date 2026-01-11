using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
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
        Endpoints = new ObservableCollection<HttpEndpoint>(settings.Endpoints);
        timeoutSeconds = settings.TimeoutSeconds;
        Endpoints.CollectionChanged += (_, _) => _settings.Endpoints = Endpoints.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "HTTP Functional";

    public ObservableCollection<HttpEndpoint> Endpoints { get; }

    [ObservableProperty]
    private int timeoutSeconds;

    /// <summary>
    /// Синхронизирует таймаут запросов.
    /// </summary>
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;
}

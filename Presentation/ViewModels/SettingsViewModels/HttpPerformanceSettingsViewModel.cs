using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpPerformance;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class HttpPerformanceSettingsViewModel : ObservableObject, ISettingsViewModel
{
    [ObservableProperty]
    private string url = "https://example.com";

    [ObservableProperty]
    private string method = "GET";

    [ObservableProperty]
    private int totalRequests = 50;

    [ObservableProperty]
    private int concurrency = 5;

    [ObservableProperty]
    private int? rpsLimit = 20;

    [ObservableProperty]
    private int timeoutSeconds = 20;

    public object BuildSettings()
    {
        return new HttpPerformanceSettings
        {
            Url = Url,
            Method = Method,
            TotalRequests = TotalRequests,
            Concurrency = Concurrency,
            RpsLimit = RpsLimit,
            TimeoutSeconds = TimeoutSeconds
        };
    }
}

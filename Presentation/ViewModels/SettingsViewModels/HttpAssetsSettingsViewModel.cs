using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpAssets;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class HttpAssetsSettingsViewModel : ObservableObject, ISettingsViewModel
{
    public ObservableCollection<string> Assets { get; } = new() { "https://example.com" };

    [ObservableProperty]
    private string? expectedContentType;

    [ObservableProperty]
    private int? maxSizeBytes;

    [ObservableProperty]
    private int? maxLatencyMs;

    [ObservableProperty]
    private int timeoutSeconds = 20;

    public object BuildSettings()
    {
        return new HttpAssetsSettings
        {
            Assets = Assets.ToList(),
            ExpectedContentType = ExpectedContentType,
            MaxSizeBytes = MaxSizeBytes,
            MaxLatencyMs = MaxLatencyMs,
            TimeoutSeconds = TimeoutSeconds
        };
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.UiTiming;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class UiTimingSettingsViewModel : ObservableObject, ISettingsViewModel
{
    public ObservableCollection<string> Urls { get; } = new() { "https://example.com" };

    [ObservableProperty]
    private int repeatsPerUrl = 3;

    [ObservableProperty]
    private int concurrency = 2;

    [ObservableProperty]
    private string waitUntil = "domcontentloaded";

    [ObservableProperty]
    private bool headless = true;

    public object BuildSettings()
    {
        return new UiTimingSettings
        {
            Urls = Urls.ToList(),
            RepeatsPerUrl = RepeatsPerUrl,
            Concurrency = Concurrency,
            WaitUntil = WaitUntil,
            Headless = Headless
        };
    }
}

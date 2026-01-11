using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.UiSnapshot;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class UiSnapshotSettingsViewModel : ObservableObject, ISettingsViewModel
{
    public ObservableCollection<string> Urls { get; } = new() { "https://example.com" };

    [ObservableProperty]
    private int concurrency = 2;

    [ObservableProperty]
    private string waitMode = "domcontentloaded";

    [ObservableProperty]
    private int delayAfterLoadMs = 0;

    [ObservableProperty]
    private bool headless = true;

    public object BuildSettings()
    {
        return new UiSnapshotSettings
        {
            Urls = Urls.ToList(),
            Concurrency = Concurrency,
            WaitMode = WaitMode,
            DelayAfterLoadMs = DelayAfterLoadMs,
            Headless = Headless
        };
    }
}

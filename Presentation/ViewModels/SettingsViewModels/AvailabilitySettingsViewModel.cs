using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.Availability;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class AvailabilitySettingsViewModel : ObservableObject, ISettingsViewModel
{
    [ObservableProperty]
    private string target = "https://example.com";

    [ObservableProperty]
    private string targetType = "Http";

    [ObservableProperty]
    private int intervalSeconds = 5;

    [ObservableProperty]
    private int durationMinutes = 1;

    [ObservableProperty]
    private int timeoutMs = 5000;

    [ObservableProperty]
    private int? failThreshold;

    public object BuildSettings()
    {
        return new AvailabilitySettings
        {
            Target = Target,
            TargetType = TargetType,
            IntervalSeconds = IntervalSeconds,
            DurationMinutes = DurationMinutes,
            TimeoutMs = TimeoutMs,
            FailThreshold = FailThreshold
        };
    }
}

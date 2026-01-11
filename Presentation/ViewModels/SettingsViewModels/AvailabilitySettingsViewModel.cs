using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.Availability;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class AvailabilitySettingsViewModel : SettingsViewModelBase
{
    private readonly AvailabilitySettings _settings;

    public AvailabilitySettingsViewModel(AvailabilitySettings settings)
    {
        _settings = settings;
        target = settings.Target;
        targetType = settings.TargetType;
        intervalSeconds = settings.IntervalSeconds;
        durationMinutes = settings.DurationMinutes;
        timeoutMs = settings.TimeoutMs;
        failThreshold = settings.FailThreshold;
    }

    public override object Settings => _settings;
    public override string Title => "Availability";

    [ObservableProperty]
    private string target = string.Empty;

    [ObservableProperty]
    private string targetType = "Http";

    [ObservableProperty]
    private int intervalSeconds;

    [ObservableProperty]
    private int durationMinutes;

    [ObservableProperty]
    private int timeoutMs;

    [ObservableProperty]
    private int failThreshold;

    partial void OnTargetChanged(string value) => _settings.Target = value;
    partial void OnTargetTypeChanged(string value) => _settings.TargetType = value;
    partial void OnIntervalSecondsChanged(int value) => _settings.IntervalSeconds = value;
    partial void OnDurationMinutesChanged(int value) => _settings.DurationMinutes = value;
    partial void OnTimeoutMsChanged(int value) => _settings.TimeoutMs = value;
    partial void OnFailThresholdChanged(int value) => _settings.FailThreshold = value;
}

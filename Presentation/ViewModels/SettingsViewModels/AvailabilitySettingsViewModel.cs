using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.Availability;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек доступности.
/// </summary>
public partial class AvailabilitySettingsViewModel : SettingsViewModelBase
{
    private readonly AvailabilitySettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public AvailabilitySettingsViewModel(AvailabilitySettings settings)
    {
        _settings = settings;
        target = settings.Target;
        targetType = settings.TargetType;
        intervalSeconds = settings.IntervalSeconds;
        durationSeconds = settings.DurationSeconds;
        timeoutMs = settings.TimeoutMs;
        failThreshold = settings.FailThreshold;
    }

    public override object Settings => _settings;
    public override string Title => "Доступность";
    public override void UpdateFrom(object settings)
    {
        if (settings is not AvailabilitySettings s)
        {
            return;
        }

        Target = s.Target;
        TargetType = s.TargetType;
        IntervalSeconds = s.IntervalSeconds;
        DurationSeconds = s.DurationSeconds;
        TimeoutMs = s.TimeoutMs;
        FailThreshold = s.FailThreshold;
    }

    [ObservableProperty]
    private string target = string.Empty;

    [ObservableProperty]
    private string targetType = "Http";

    [ObservableProperty]
    private int intervalSeconds;

    [ObservableProperty]
    private int durationSeconds;

    [ObservableProperty]
    private int timeoutMs;

    [ObservableProperty]
    private int failThreshold;

    /// <summary>
    /// Синхронизирует URL цели.
    /// </summary>
    partial void OnTargetChanged(string value) => _settings.Target = value;
    /// <summary>
    /// Синхронизирует тип цели (HTTP/TCP).
    /// </summary>
    partial void OnTargetTypeChanged(string value) => _settings.TargetType = value;
    /// <summary>
    /// Синхронизирует интервал проверок.
    /// </summary>
    partial void OnIntervalSecondsChanged(int value) => _settings.IntervalSeconds = value;
    /// <summary>
    /// Синхронизирует длительность теста.
    /// </summary>
    partial void OnDurationSecondsChanged(int value) => _settings.DurationSeconds = value;
    /// <summary>
    /// Синхронизирует таймаут запросов.
    /// </summary>
    partial void OnTimeoutMsChanged(int value) => _settings.TimeoutMs = value;
    /// <summary>
    /// Синхронизирует порог последовательных ошибок.
    /// </summary>
    partial void OnFailThresholdChanged(int value) => _settings.FailThreshold = value;
}

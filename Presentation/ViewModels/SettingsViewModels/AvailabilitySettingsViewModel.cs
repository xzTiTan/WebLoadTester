using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.Availability;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class AvailabilitySettingsViewModel : SettingsViewModelBase
{
    private readonly AvailabilitySettings _settings;

    public AvailabilitySettingsViewModel(AvailabilitySettings settings)
    {
        _settings = settings;
        _settings.NormalizeLegacy();
        checkType = settings.CheckType;
        url = settings.Url;
        host = settings.Host;
        port = settings.Port;
        timeoutMs = settings.TimeoutMs;
    }

    public override object Settings => _settings;
    public override string Title => "Доступность";

    public override void UpdateFrom(object settings)
    {
        if (settings is not AvailabilitySettings s)
        {
            return;
        }

        s.NormalizeLegacy();
        CheckType = s.CheckType;
        Url = s.Url;
        Host = s.Host;
        Port = s.Port;
        TimeoutMs = s.TimeoutMs;
    }

    [ObservableProperty] private string checkType = "HTTP";
    [ObservableProperty] private string url = string.Empty;
    [ObservableProperty] private string host = string.Empty;
    [ObservableProperty] private int port = 443;
    [ObservableProperty] private int timeoutMs;

    public string[] CheckTypeOptions { get; } = { "HTTP", "TCP" };
    public bool IsHttp => CheckType.Equals("HTTP", System.StringComparison.OrdinalIgnoreCase);
    public bool IsTcp => CheckType.Equals("TCP", System.StringComparison.OrdinalIgnoreCase);

    partial void OnCheckTypeChanged(string value)
    {
        _settings.CheckType = value;
        OnPropertyChanged(nameof(IsHttp));
        OnPropertyChanged(nameof(IsTcp));
    }

    partial void OnUrlChanged(string value) => _settings.Url = value;
    partial void OnHostChanged(string value) => _settings.Host = value;
    partial void OnPortChanged(int value) => _settings.Port = value;
    partial void OnTimeoutMsChanged(int value) => _settings.TimeoutMs = value;
}

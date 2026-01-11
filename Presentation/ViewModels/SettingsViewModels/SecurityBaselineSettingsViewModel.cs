using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.SecurityBaseline;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class SecurityBaselineSettingsViewModel : SettingsViewModelBase
{
    private readonly SecurityBaselineSettings _settings;

    public SecurityBaselineSettingsViewModel(SecurityBaselineSettings settings)
    {
        _settings = settings;
        url = settings.Url;
        checkHeaders = settings.CheckHeaders;
        checkRedirectHttpToHttps = settings.CheckRedirectHttpToHttps;
        checkTlsExpiry = settings.CheckTlsExpiry;
    }

    public override object Settings => _settings;
    public override string Title => "Security Baseline";

    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    private bool checkHeaders;

    [ObservableProperty]
    private bool checkRedirectHttpToHttps;

    [ObservableProperty]
    private bool checkTlsExpiry;

    partial void OnUrlChanged(string value) => _settings.Url = value;
    partial void OnCheckHeadersChanged(bool value) => _settings.CheckHeaders = value;
    partial void OnCheckRedirectHttpToHttpsChanged(bool value) => _settings.CheckRedirectHttpToHttps = value;
    partial void OnCheckTlsExpiryChanged(bool value) => _settings.CheckTlsExpiry = value;
}

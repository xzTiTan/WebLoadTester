using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.SecurityBaseline;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек Security Baseline.
/// </summary>
public partial class SecurityBaselineSettingsViewModel : SettingsViewModelBase
{
    private readonly SecurityBaselineSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public SecurityBaselineSettingsViewModel(SecurityBaselineSettings settings)
    {
        _settings = settings;
        url = settings.Url;
        checkHeaders = settings.CheckHeaders;
        checkRedirectHttpToHttps = settings.CheckRedirectHttpToHttps;
        checkTlsExpiry = settings.CheckTlsExpiry;
    }

    public override object Settings => _settings;
    public override string Title => "Базовая безопасность";

    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    private bool checkHeaders;

    [ObservableProperty]
    private bool checkRedirectHttpToHttps;

    [ObservableProperty]
    private bool checkTlsExpiry;

    /// <summary>
    /// Синхронизирует URL проверки.
    /// </summary>
    partial void OnUrlChanged(string value) => _settings.Url = value;
    /// <summary>
    /// Синхронизирует флаг проверки заголовков.
    /// </summary>
    partial void OnCheckHeadersChanged(bool value) => _settings.CheckHeaders = value;
    /// <summary>
    /// Синхронизирует флаг проверки редиректа HTTP→HTTPS.
    /// </summary>
    partial void OnCheckRedirectHttpToHttpsChanged(bool value) => _settings.CheckRedirectHttpToHttps = value;
    /// <summary>
    /// Синхронизирует флаг проверки срока TLS.
    /// </summary>
    partial void OnCheckTlsExpiryChanged(bool value) => _settings.CheckTlsExpiry = value;
}

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
        checkHsts = settings.CheckHsts;
        checkContentTypeOptions = settings.CheckContentTypeOptions;
        checkFrameOptions = settings.CheckFrameOptions;
        checkContentSecurityPolicy = settings.CheckContentSecurityPolicy;
        checkReferrerPolicy = settings.CheckReferrerPolicy;
        checkPermissionsPolicy = settings.CheckPermissionsPolicy;
        checkRedirectHttpToHttps = settings.CheckRedirectHttpToHttps;
    }

    public override object Settings => _settings;
    public override string Title => "Базовая безопасность";
    public override void UpdateFrom(object settings)
    {
        if (settings is not SecurityBaselineSettings s)
        {
            return;
        }

        Url = s.Url;
        CheckHsts = s.CheckHsts;
        CheckContentTypeOptions = s.CheckContentTypeOptions;
        CheckFrameOptions = s.CheckFrameOptions;
        CheckContentSecurityPolicy = s.CheckContentSecurityPolicy;
        CheckReferrerPolicy = s.CheckReferrerPolicy;
        CheckPermissionsPolicy = s.CheckPermissionsPolicy;
        CheckRedirectHttpToHttps = s.CheckRedirectHttpToHttps;
    }

    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    private bool checkHsts;

    [ObservableProperty]
    private bool checkContentTypeOptions;

    [ObservableProperty]
    private bool checkFrameOptions;

    [ObservableProperty]
    private bool checkContentSecurityPolicy;

    [ObservableProperty]
    private bool checkReferrerPolicy;

    [ObservableProperty]
    private bool checkPermissionsPolicy;

    [ObservableProperty]
    private bool checkRedirectHttpToHttps;

    /// <summary>
    /// Синхронизирует URL проверки.
    /// </summary>
    partial void OnUrlChanged(string value) => _settings.Url = value;
    /// <summary>
    /// Синхронизирует флаг проверки HSTS.
    /// </summary>
    partial void OnCheckHstsChanged(bool value) => _settings.CheckHsts = value;
    /// <summary>
    /// Синхронизирует флаг проверки X-Content-Type-Options.
    /// </summary>
    partial void OnCheckContentTypeOptionsChanged(bool value) => _settings.CheckContentTypeOptions = value;
    /// <summary>
    /// Синхронизирует флаг проверки X-Frame-Options.
    /// </summary>
    partial void OnCheckFrameOptionsChanged(bool value) => _settings.CheckFrameOptions = value;
    /// <summary>
    /// Синхронизирует флаг проверки CSP.
    /// </summary>
    partial void OnCheckContentSecurityPolicyChanged(bool value) => _settings.CheckContentSecurityPolicy = value;
    /// <summary>
    /// Синхронизирует флаг проверки Referrer-Policy.
    /// </summary>
    partial void OnCheckReferrerPolicyChanged(bool value) => _settings.CheckReferrerPolicy = value;
    /// <summary>
    /// Синхронизирует флаг проверки Permissions-Policy.
    /// </summary>
    partial void OnCheckPermissionsPolicyChanged(bool value) => _settings.CheckPermissionsPolicy = value;
    /// <summary>
    /// Синхронизирует флаг проверки редиректа HTTP→HTTPS.
    /// </summary>
    partial void OnCheckRedirectHttpToHttpsChanged(bool value) => _settings.CheckRedirectHttpToHttps = value;
}

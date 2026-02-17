using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.SecurityBaseline;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class SecurityBaselineSettingsViewModel : SettingsViewModelBase
{
    private readonly SecurityBaselineSettings _settings;

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
        checkCookieFlags = settings.CheckCookieFlags;
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
        CheckCookieFlags = s.CheckCookieFlags;
    }

    [ObservableProperty] private string url = string.Empty;
    [ObservableProperty] private bool checkHsts;
    [ObservableProperty] private bool checkContentTypeOptions;
    [ObservableProperty] private bool checkFrameOptions;
    [ObservableProperty] private bool checkContentSecurityPolicy;
    [ObservableProperty] private bool checkReferrerPolicy;
    [ObservableProperty] private bool checkPermissionsPolicy;
    [ObservableProperty] private bool checkRedirectHttpToHttps;
    [ObservableProperty] private bool checkCookieFlags;

    partial void OnUrlChanged(string value) => _settings.Url = value;
    partial void OnCheckHstsChanged(bool value) => _settings.CheckHsts = value;
    partial void OnCheckContentTypeOptionsChanged(bool value) => _settings.CheckContentTypeOptions = value;
    partial void OnCheckFrameOptionsChanged(bool value) => _settings.CheckFrameOptions = value;
    partial void OnCheckContentSecurityPolicyChanged(bool value) => _settings.CheckContentSecurityPolicy = value;
    partial void OnCheckReferrerPolicyChanged(bool value) => _settings.CheckReferrerPolicy = value;
    partial void OnCheckPermissionsPolicyChanged(bool value) => _settings.CheckPermissionsPolicy = value;
    partial void OnCheckRedirectHttpToHttpsChanged(bool value) => _settings.CheckRedirectHttpToHttps = value;
    partial void OnCheckCookieFlagsChanged(bool value) => _settings.CheckCookieFlags = value;

    [RelayCommand]
    private void SelectRecommended()
    {
        CheckHsts = true;
        CheckContentTypeOptions = true;
        CheckFrameOptions = true;
        CheckContentSecurityPolicy = true;
        CheckReferrerPolicy = true;
        CheckPermissionsPolicy = true;
        CheckRedirectHttpToHttps = true;
        CheckCookieFlags = true;
    }
}

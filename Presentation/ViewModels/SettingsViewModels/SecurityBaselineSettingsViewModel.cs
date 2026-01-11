using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.SecurityBaseline;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class SecurityBaselineSettingsViewModel : ObservableObject, ISettingsViewModel
{
    [ObservableProperty]
    private string url = "https://example.com";

    [ObservableProperty]
    private bool checkHeaders = true;

    [ObservableProperty]
    private bool checkRedirectHttpToHttps;

    [ObservableProperty]
    private bool checkTlsExpiry;

    public object BuildSettings()
    {
        return new SecurityBaselineSettings
        {
            Url = Url,
            CheckHeaders = CheckHeaders,
            CheckRedirectHttpToHttps = CheckRedirectHttpToHttps,
            CheckTlsExpiry = CheckTlsExpiry
        };
    }
}

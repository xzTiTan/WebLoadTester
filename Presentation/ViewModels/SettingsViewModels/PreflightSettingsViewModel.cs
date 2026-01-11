using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.Preflight;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class PreflightSettingsViewModel : ObservableObject, ISettingsViewModel
{
    [ObservableProperty]
    private string target = "https://example.com";

    [ObservableProperty]
    private bool checkDns = true;

    [ObservableProperty]
    private bool checkTcp = true;

    [ObservableProperty]
    private bool checkTls = true;

    [ObservableProperty]
    private bool checkHttp = true;

    public object BuildSettings()
    {
        return new PreflightSettings
        {
            Target = Target,
            CheckDns = CheckDns,
            CheckTcp = CheckTcp,
            CheckTls = CheckTls,
            CheckHttp = CheckHttp
        };
    }
}

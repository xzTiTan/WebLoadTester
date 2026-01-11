using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.Preflight;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class PreflightSettingsViewModel : SettingsViewModelBase
{
    private readonly PreflightSettings _settings;

    public PreflightSettingsViewModel(PreflightSettings settings)
    {
        _settings = settings;
        target = settings.Target;
        checkDns = settings.CheckDns;
        checkTcp = settings.CheckTcp;
        checkTls = settings.CheckTls;
        checkHttp = settings.CheckHttp;
    }

    public override object Settings => _settings;
    public override string Title => "Preflight";

    [ObservableProperty]
    private string target = string.Empty;

    [ObservableProperty]
    private bool checkDns;

    [ObservableProperty]
    private bool checkTcp;

    [ObservableProperty]
    private bool checkTls;

    [ObservableProperty]
    private bool checkHttp;

    partial void OnTargetChanged(string value) => _settings.Target = value;
    partial void OnCheckDnsChanged(bool value) => _settings.CheckDns = value;
    partial void OnCheckTcpChanged(bool value) => _settings.CheckTcp = value;
    partial void OnCheckTlsChanged(bool value) => _settings.CheckTls = value;
    partial void OnCheckHttpChanged(bool value) => _settings.CheckHttp = value;
}

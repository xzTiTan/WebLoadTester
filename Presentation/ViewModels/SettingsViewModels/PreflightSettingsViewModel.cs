using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.Preflight;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек preflight-проверок.
/// </summary>
public partial class PreflightSettingsViewModel : SettingsViewModelBase
{
    private readonly PreflightSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
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
    public override string Title => "Предварительные проверки";

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

    /// <summary>
    /// Синхронизирует цель проверок.
    /// </summary>
    partial void OnTargetChanged(string value) => _settings.Target = value;
    /// <summary>
    /// Синхронизирует флаг DNS-проверки.
    /// </summary>
    partial void OnCheckDnsChanged(bool value) => _settings.CheckDns = value;
    /// <summary>
    /// Синхронизирует флаг TCP-проверки.
    /// </summary>
    partial void OnCheckTcpChanged(bool value) => _settings.CheckTcp = value;
    /// <summary>
    /// Синхронизирует флаг TLS-проверки.
    /// </summary>
    partial void OnCheckTlsChanged(bool value) => _settings.CheckTls = value;
    /// <summary>
    /// Синхронизирует флаг HTTP-проверки.
    /// </summary>
    partial void OnCheckHttpChanged(bool value) => _settings.CheckHttp = value;
}

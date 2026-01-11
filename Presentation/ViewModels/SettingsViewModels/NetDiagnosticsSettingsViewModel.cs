using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.NetDiagnostics;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class NetDiagnosticsSettingsViewModel : SettingsViewModelBase
{
    private readonly NetDiagnosticsSettings _settings;

    public NetDiagnosticsSettingsViewModel(NetDiagnosticsSettings settings)
    {
        _settings = settings;
        hostname = settings.Hostname;
        Ports = new ObservableCollection<int>(settings.Ports);
        enableDns = settings.EnableDns;
        enableTcp = settings.EnableTcp;
        enableTls = settings.EnableTls;
        Ports.CollectionChanged += (_, _) => _settings.Ports = Ports.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "Network Diagnostics";

    public ObservableCollection<int> Ports { get; }

    [ObservableProperty]
    private string hostname = string.Empty;

    [ObservableProperty]
    private bool enableDns;

    [ObservableProperty]
    private bool enableTcp;

    [ObservableProperty]
    private bool enableTls;

    partial void OnHostnameChanged(string value) => _settings.Hostname = value;
    partial void OnEnableDnsChanged(bool value) => _settings.EnableDns = value;
    partial void OnEnableTcpChanged(bool value) => _settings.EnableTcp = value;
    partial void OnEnableTlsChanged(bool value) => _settings.EnableTls = value;
}

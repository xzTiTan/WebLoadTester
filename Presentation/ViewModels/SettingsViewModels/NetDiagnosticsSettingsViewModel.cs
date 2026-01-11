using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.NetDiagnostics;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class NetDiagnosticsSettingsViewModel : ObservableObject, ISettingsViewModel
{
    public ObservableCollection<int> Ports { get; } = new() { 80, 443 };

    [ObservableProperty]
    private string hostname = "example.com";

    [ObservableProperty]
    private bool enableDns = true;

    [ObservableProperty]
    private bool enableTcp = true;

    [ObservableProperty]
    private bool enableTls = true;

    public object BuildSettings()
    {
        return new NetDiagnosticsSettings
        {
            Hostname = Hostname,
            Ports = Ports.ToList(),
            EnableDns = EnableDns,
            EnableTcp = EnableTcp,
            EnableTls = EnableTls
        };
    }
}

using WebLoadTester.Modules.NetDiagnostics;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public sealed class NetDiagnosticsSettingsViewModel : SettingsViewModelBase
{
    public NetDiagnosticsSettingsViewModel(NetDiagnosticsSettings settings)
    {
        Model = settings;
    }

    public NetDiagnosticsSettings Model { get; }
    public override object Settings => Model;
}

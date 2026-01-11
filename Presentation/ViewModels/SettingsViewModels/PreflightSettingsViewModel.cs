using WebLoadTester.Modules.Preflight;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public sealed class PreflightSettingsViewModel : SettingsViewModelBase
{
    public PreflightSettingsViewModel(PreflightSettings settings)
    {
        Model = settings;
    }

    public PreflightSettings Model { get; }
    public override object Settings => Model;
}

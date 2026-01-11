using WebLoadTester.Modules.SecurityBaseline;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public sealed class SecurityBaselineSettingsViewModel : SettingsViewModelBase
{
    public SecurityBaselineSettingsViewModel(SecurityBaselineSettings settings)
    {
        Model = settings;
    }

    public SecurityBaselineSettings Model { get; }
    public override object Settings => Model;
}

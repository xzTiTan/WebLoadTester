using WebLoadTester.Modules.UiScenario;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public sealed class UiScenarioSettingsViewModel : SettingsViewModelBase
{
    public UiScenarioSettingsViewModel(UiScenarioSettings settings)
    {
        Model = settings;
    }

    public UiScenarioSettings Model { get; }
    public override object Settings => Model;
}

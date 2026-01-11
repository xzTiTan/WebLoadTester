using WebLoadTester.Modules.Availability;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public sealed class AvailabilitySettingsViewModel : SettingsViewModelBase
{
    public AvailabilitySettingsViewModel(AvailabilitySettings settings)
    {
        Model = settings;
    }

    public AvailabilitySettings Model { get; }
    public override object Settings => Model;
}

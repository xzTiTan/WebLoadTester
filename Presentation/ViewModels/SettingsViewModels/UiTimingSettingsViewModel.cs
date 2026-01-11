using WebLoadTester.Modules.UiTiming;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public sealed class UiTimingSettingsViewModel : SettingsViewModelBase
{
    public UiTimingSettingsViewModel(UiTimingSettings settings)
    {
        Model = settings;
    }

    public UiTimingSettings Model { get; }
    public override object Settings => Model;
}

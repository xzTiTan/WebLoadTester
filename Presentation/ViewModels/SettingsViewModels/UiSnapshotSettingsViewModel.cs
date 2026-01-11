using WebLoadTester.Modules.UiSnapshot;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public sealed class UiSnapshotSettingsViewModel : SettingsViewModelBase
{
    public UiSnapshotSettingsViewModel(UiSnapshotSettings settings)
    {
        Model = settings;
    }

    public UiSnapshotSettings Model { get; }
    public override object Settings => Model;
}

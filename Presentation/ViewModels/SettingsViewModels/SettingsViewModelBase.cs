using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public abstract class SettingsViewModelBase : ViewModelBase
{
    public abstract object Settings { get; }
}

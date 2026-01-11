using WebLoadTester.Core.Contracts;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;

namespace WebLoadTester.Presentation.ViewModels;

public class ModuleItemViewModel
{
    public ModuleItemViewModel(ITestModule module, SettingsViewModelBase settingsViewModel)
    {
        Module = module;
        SettingsViewModel = settingsViewModel;
    }

    public ITestModule Module { get; }
    public SettingsViewModelBase SettingsViewModel { get; }
    public string DisplayName => Module.DisplayName;
}

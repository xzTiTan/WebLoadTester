using WebLoadTester.Core.Contracts;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;

namespace WebLoadTester.Presentation.ViewModels;

public sealed class ModuleEntry
{
    public ModuleEntry(ITestModule module, ISettingsViewModel settingsViewModel)
    {
        Module = module;
        SettingsViewModel = settingsViewModel;
    }

    public ITestModule Module { get; }
    public ISettingsViewModel SettingsViewModel { get; }
    public string DisplayName => Module.DisplayName;
}

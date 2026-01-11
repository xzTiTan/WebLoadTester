using WebLoadTester.Core.Contracts;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// Представление модуля и его настроек для списка в UI.
/// </summary>
public class ModuleItemViewModel
{
    /// <summary>
    /// Создаёт ViewModel элемента модуля.
    /// </summary>
    public ModuleItemViewModel(ITestModule module, SettingsViewModelBase settingsViewModel)
    {
        Module = module;
        SettingsViewModel = settingsViewModel;
    }

    public ITestModule Module { get; }
    public SettingsViewModelBase SettingsViewModel { get; }
    /// <summary>
    /// Отображаемое имя модуля.
    /// </summary>
    public string DisplayName => Module.DisplayName;
}

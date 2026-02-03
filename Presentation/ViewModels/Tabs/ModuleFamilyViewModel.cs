using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.Presentation.ViewModels.Tabs;

/// <summary>
/// ViewModel семейства модулей для вкладок UI.
/// </summary>
public partial class ModuleFamilyViewModel : ObservableObject
{
    /// <summary>
    /// Создаёт семейство модулей с заголовком и списком.
    /// </summary>
    public ModuleFamilyViewModel(string title, ObservableCollection<ModuleItemViewModel> modules)
    {
        Title = title;
        Modules = modules;
        selectedModule = modules.Count > 0 ? modules[0] : null;
    }

    public string Title { get; }
    public ObservableCollection<ModuleItemViewModel> Modules { get; }

    [ObservableProperty]
    private ModuleItemViewModel? selectedModule;
}

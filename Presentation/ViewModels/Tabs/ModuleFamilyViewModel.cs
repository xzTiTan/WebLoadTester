using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    public IEnumerable<ModuleItemViewModel> FilteredModules =>
        string.IsNullOrWhiteSpace(ModuleFilterText)
            ? Modules
            : Modules.Where(module => MatchesFilter(module.DisplayName));

    [ObservableProperty]
    private ModuleItemViewModel? selectedModule;

    [ObservableProperty]
    private string moduleFilterText = string.Empty;

    partial void OnModuleFilterTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredModules));
        if (SelectedModule != null && MatchesFilter(SelectedModule.DisplayName))
        {
            return;
        }

        SelectedModule = FilteredModules.FirstOrDefault();
    }

    private bool MatchesFilter(string name) =>
        name.Contains(ModuleFilterText ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}

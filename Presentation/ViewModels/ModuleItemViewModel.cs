using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// Представление модуля и его настроек для списка в UI.
/// </summary>
public partial class ModuleItemViewModel : ObservableObject
{
    /// <summary>
    /// Создаёт ViewModel элемента модуля.
    /// </summary>
    public ModuleItemViewModel(ITestModule module, SettingsViewModelBase settingsViewModel, TestLibraryViewModel testLibrary)
    {
        Module = module;
        SettingsViewModel = settingsViewModel;
        TestLibrary = testLibrary;
    }

    public ITestModule Module { get; }
    public SettingsViewModelBase SettingsViewModel { get; }
    public TestLibraryViewModel TestLibrary { get; }
    /// <summary>
    /// Отображаемое имя модуля.
    /// </summary>
    public string DisplayName => Module.DisplayName;
    public string Description => Module.Description;

    [ObservableProperty]
    private TestReport? lastReport;

    public bool HasReport => LastReport != null;

    partial void OnLastReportChanged(TestReport? value)
    {
        OnPropertyChanged(nameof(HasReport));
    }
}

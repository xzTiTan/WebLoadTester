using System.Collections.ObjectModel;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.ViewModels.Tabs;

public sealed class UiTestingTabViewModel : ViewModelBase
{
    public UiTestingTabViewModel(ObservableCollection<ModuleViewModel> modules)
    {
        Modules = modules;
    }

    public ObservableCollection<ModuleViewModel> Modules { get; }
}

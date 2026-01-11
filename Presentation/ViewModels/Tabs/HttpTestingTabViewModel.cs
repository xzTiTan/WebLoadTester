using System.Collections.ObjectModel;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.ViewModels.Tabs;

public sealed class HttpTestingTabViewModel : ViewModelBase
{
    public HttpTestingTabViewModel(ObservableCollection<ModuleViewModel> modules)
    {
        Modules = modules;
    }

    public ObservableCollection<ModuleViewModel> Modules { get; }
}

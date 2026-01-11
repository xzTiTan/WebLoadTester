using System.Collections.ObjectModel;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.ViewModels.Tabs;

public sealed class NetSecTabViewModel : ViewModelBase
{
    public NetSecTabViewModel(ObservableCollection<ModuleViewModel> modules)
    {
        Modules = modules;
    }

    public ObservableCollection<ModuleViewModel> Modules { get; }
}

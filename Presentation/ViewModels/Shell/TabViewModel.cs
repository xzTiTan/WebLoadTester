using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.Presentation.ViewModels.Shell;

public partial class TabViewModel : ObservableObject
{
    public TabViewModel(string title, object contentVm)
    {
        Title = title;
        ContentVm = contentVm;
    }

    public string Title { get; }
    public object ContentVm { get; }
}

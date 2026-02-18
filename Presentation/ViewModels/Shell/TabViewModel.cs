using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.Presentation.ViewModels.Shell;

public partial class TabViewModel : ObservableObject
{
    public TabViewModel(string title, string fullTitle, object contentVm)
    {
        Title = title;
        FullTitle = fullTitle;
        ContentVm = contentVm;
    }

    public string Title { get; }
    public string FullTitle { get; }
    public object ContentVm { get; }
}

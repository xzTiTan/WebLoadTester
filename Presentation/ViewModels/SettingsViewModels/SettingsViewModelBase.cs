using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public abstract partial class SettingsViewModelBase : ObservableObject
{
    public abstract object Settings { get; }
    public abstract string Title { get; }
}

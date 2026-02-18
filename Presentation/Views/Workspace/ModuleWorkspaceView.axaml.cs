using Avalonia.Controls;
using WebLoadTester.Presentation.ViewModels.Workspace;

namespace WebLoadTester.Presentation.Views.Workspace;

public partial class ModuleWorkspaceView : UserControl
{
    private ModuleFamilyViewModel? _vm;
    private ScrollViewer? _scrollViewer;

    public ModuleWorkspaceView()
    {
        InitializeComponent();
        _scrollViewer = this.FindControl<ScrollViewer>("WorkspaceScrollViewer");
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_vm?.Workspace != null)
        {
            _vm.Workspace.PropertyChanged -= OnWorkspacePropertyChanged;
        }

        _vm = DataContext as ModuleFamilyViewModel;
        if (_vm?.Workspace != null)
        {
            _vm.Workspace.PropertyChanged += OnWorkspacePropertyChanged;
        }
    }

    private void OnWorkspacePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModuleWorkspaceViewModel.ScrollToTopRequestToken))
        {
            _scrollViewer?.ScrollToHome();
        }
    }
}

using Avalonia.Controls;
using Avalonia.Interactivity;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.Views;

public partial class ModuleWorkspaceView : UserControl
{
    public ModuleWorkspaceView()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnConfigNameLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.SelectedModule?.ModuleConfig.MarkFieldTouchedCommand.Execute(ModuleConfigViewModel.ConfigNameKey);
    }

    private void OnProfileParallelismLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.RunProfile.MarkFieldTouched(RunProfileViewModel.ParallelismKey);
    }

    private void OnProfileIterationsLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.RunProfile.MarkFieldTouched(RunProfileViewModel.IterationsKey);
    }

    private void OnProfileDurationLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.RunProfile.MarkFieldTouched(RunProfileViewModel.DurationKey);
    }

    private void OnProfileTimeoutLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.RunProfile.MarkFieldTouched(RunProfileViewModel.TimeoutKey);
    }

    private void OnProfilePauseLostFocus(object? sender, RoutedEventArgs e)
    {
        Vm?.RunProfile.MarkFieldTouched(RunProfileViewModel.PauseKey);
    }
}

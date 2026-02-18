using Avalonia.Controls;
using Avalonia.Input;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.Views.Tabs;

public partial class RunsTab : UserControl
{
    public RunsTab()
    {
        InitializeComponent();
        RunsGrid.AddHandler(DoubleTappedEvent, OnRunsGridDoubleTapped, handledEventsToo: true);
    }

    private void OnRunsGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is RunsTabViewModel vm && vm.HasSelectedRun && !vm.IsRunning)
        {
            vm.OpenJsonCommand.Execute(null);
            e.Handled = true;
        }
    }
}

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
        if (DataContext is RunsTabViewModel vm && vm.HasValidSelection && !vm.IsRunning)
        {
            vm.OpenHtmlCommand.Execute(null);
            e.Handled = true;
        }
    }
}

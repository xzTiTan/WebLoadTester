using Avalonia.Controls;
using Avalonia.Input;
using WebLoadTester.Presentation.ViewModels.Shell;

namespace WebLoadTester.Presentation.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new AppShellViewModel();
        KeyDown += OnMainWindowKeyDown;
    }

    private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not AppShellViewModel vm)
        {
            return;
        }

        if (e.Source is TextBox)
        {
            return;
        }

        if (vm.LogDrawer.IsExpanded)
        {
            vm.LogDrawer.IsExpanded = false;
            e.Handled = true;
        }
    }
}

using Avalonia.Controls;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}

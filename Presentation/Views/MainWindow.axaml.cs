using Avalonia.Controls;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.Views;

/// <summary>
/// Главное окно приложения.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Инициализирует компоненты окна и назначает DataContext.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}

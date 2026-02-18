using Avalonia.Controls;
using WebLoadTester.Presentation.ViewModels.Workspace;

namespace WebLoadTester.Presentation.Views.Workspace;

public partial class RunControlView : UserControl
{
    private RunControlViewModel? _vm;
    private Button? _startButton;

    public RunControlView()
    {
        InitializeComponent();
        _startButton = this.FindControl<Button>("StartButton");
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as RunControlViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RunControlViewModel.FocusRequestToken) && _startButton != null)
        {
            _startButton.Focus();
        }
    }
}

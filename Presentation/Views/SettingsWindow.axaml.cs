using System;
using Avalonia.Controls;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.Views;

public partial class SettingsWindow : Window
{
    private SettingsWindowViewModel? _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as SettingsWindowViewModel;
        if (_viewModel != null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel = null;
        }
    }

    private void OnCloseRequested()
    {
        Close();
    }
}

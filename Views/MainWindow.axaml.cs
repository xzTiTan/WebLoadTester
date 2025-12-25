using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Specialized;
using WebLoadTester.ViewModels;

namespace WebLoadTester.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ListBox? _logListBox;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        _logListBox = this.FindControl<ListBox>("LogListBox");

        _viewModel.LogEntries.CollectionChanged += OnLogEntriesChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.LogEntries.CollectionChanged -= OnLogEntriesChanged;
        _viewModel.Dispose();
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_logListBox == null || _logListBox.ItemCount == 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var lastIndex = _logListBox.ItemCount - 1;
            _logListBox.ScrollIntoView(lastIndex);
        });
    }
}

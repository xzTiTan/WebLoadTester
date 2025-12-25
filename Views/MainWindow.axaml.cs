using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Collections.Specialized;
using Avalonia.VisualTree;
using WebLoadTester.Domain;
using WebLoadTester.ViewModels;

namespace WebLoadTester.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ListBox? _logListBox;
    private readonly ListBox? _selectorListBox;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        _logListBox = this.FindControl<ListBox>("LogListBox");
        _selectorListBox = this.FindControl<ListBox>("SelectorListBox");

        _viewModel.LogEntries.CollectionChanged += OnLogEntriesChanged;
        _viewModel.RequestFocusSelector += OnRequestFocusSelector;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.LogEntries.CollectionChanged -= OnLogEntriesChanged;
        _viewModel.RequestFocusSelector -= OnRequestFocusSelector;
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

    private void OnRequestFocusSelector(SelectorItem item)
    {
        if (_selectorListBox == null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _selectorListBox.SelectedItem = item;
            var container = _selectorListBox.ContainerFromItem(item) as Control;
            if (container != null)
            {
                var textBox = container.FindDescendantOfType<TextBox>();
                textBox?.Focus();
            }
            else
            {
                _selectorListBox.Focus();
            }
        }, DispatcherPriority.Background);
    }
}

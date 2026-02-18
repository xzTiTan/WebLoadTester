using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WebLoadTester.Presentation.ViewModels.Controls;

namespace WebLoadTester.Presentation.Views.Controls;

public partial class RowListEditorView : UserControl
{
    private const int MaxFocusAttempts = 3;

    private ListBox? _itemsList;
    private RowListEditorViewModel? _viewModel;

    public RowListEditorView()
    {
        InitializeComponent();
        _itemsList = this.FindControl<ListBox>("ItemsList");
        if (_itemsList != null)
        {
            _itemsList.SelectionChanged += OnSelectionChanged;
        }

        DataContextChanged += OnDataContextChanged;
        AttachViewModel(DataContext as RowListEditorViewModel);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachViewModel(DataContext as RowListEditorViewModel);
    }

    private void AttachViewModel(RowListEditorViewModel? viewModel)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RowListEditorViewModel.FocusRequestToken))
        {
            RequestFocusForSelectedItem();
        }
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_itemsList?.SelectedItem == null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _itemsList.ScrollIntoView(_itemsList.SelectedItem));
    }

    private void RequestFocusForSelectedItem()
    {
        TryFocusFirstEditableControlForSelectedItem(0);
    }

    private void TryFocusFirstEditableControlForSelectedItem(int attempt)
    {
        if (_itemsList?.SelectedItem == null)
        {
            return;
        }

        var selectedItem = _itemsList.SelectedItem;
        Dispatcher.UIThread.Post(() =>
        {
            if (_itemsList == null || _itemsList.SelectedItem != selectedItem)
            {
                return;
            }

            _itemsList.ScrollIntoView(selectedItem);

            var container = _itemsList.ContainerFromItem(selectedItem) as ListBoxItem;
            if (container == null)
            {
                if (attempt + 1 < MaxFocusAttempts)
                {
                    TryFocusFirstEditableControlForSelectedItem(attempt + 1);
                }

                return;
            }

            if (TryFocusFirstEditableDescendant(container))
            {
                return;
            }

            if (attempt + 1 < MaxFocusAttempts)
            {
                TryFocusFirstEditableControlForSelectedItem(attempt + 1);
            }
        }, DispatcherPriority.Background);
    }

    private static bool TryFocusFirstEditableDescendant(ListBoxItem container)
    {
        var target = container
            .GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control =>
                control.Focusable &&
                control.IsVisible &&
                control.IsEnabled &&
                (control is TextBox || control is ComboBox || control is NumericUpDown));

        return target?.Focus() == true;
    }
}

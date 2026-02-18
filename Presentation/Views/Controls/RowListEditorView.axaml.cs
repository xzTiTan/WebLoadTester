using Avalonia.Controls;
using Avalonia.Threading;

namespace WebLoadTester.Presentation.Views.Controls;

public partial class RowListEditorView : UserControl
{
    private ListBox? _itemsList;

    public RowListEditorView()
    {
        InitializeComponent();
        _itemsList = this.FindControl<ListBox>("ItemsList");
        if (_itemsList != null)
        {
            _itemsList.SelectionChanged += OnSelectionChanged;
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
}

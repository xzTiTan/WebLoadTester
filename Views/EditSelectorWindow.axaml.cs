using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace WebLoadTester.Views;

public partial class EditSelectorWindow : Window
{
    public EditSelectorWindow(string initialText)
    {
        InitializeComponent();

        SelectorTextBox.Text = initialText;
        SelectorTextBox.Focus();

        OkButton.Click += OkButton_Click;
        CancelButton.Click += (_, __) => Close(null);
        SelectorTextBox.KeyUp += SelectorTextBox_KeyUp;
    }

    private void SelectorTextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveAndClose();
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
        }
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        SaveAndClose();
    }

    private void SaveAndClose()
    {
        var text = (SelectorTextBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            Close(null);
            return;
        }

        Close(text);
    }
}

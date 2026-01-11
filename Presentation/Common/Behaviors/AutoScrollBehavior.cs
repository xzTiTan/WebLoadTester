using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace WebLoadTester.Presentation.Common.Behaviors;

public static class AutoScrollBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>("IsEnabled", typeof(AutoScrollBehavior));

    static AutoScrollBehavior()
    {
        IsEnabledProperty.Changed.Subscribe(args =>
        {
            if (args.Sender is ListBox listBox)
            {
                if ((bool)args.NewValue)
                {
                    listBox.LayoutUpdated += OnLayoutUpdated;
                }
                else
                {
                    listBox.LayoutUpdated -= OnLayoutUpdated;
                }
            }
        });
    }

    private static void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        if (listBox.ItemCount > 0)
        {
            listBox.ScrollIntoView(listBox.ItemCount - 1);
        }
    }
}

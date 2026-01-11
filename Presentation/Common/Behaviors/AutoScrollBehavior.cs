using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace WebLoadTester.Presentation.Common.Behaviors;

public static class AutoScrollBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>("IsEnabled", typeof(AutoScrollBehavior));

    static AutoScrollBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<ListBox>(OnChanged);
    }

    public static bool GetIsEnabled(AvaloniaObject element) => element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(AvaloniaObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnChanged(ListBox listBox, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.NewValue is true)
        {
            if (listBox.Items is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged += (_, _) => ScrollToEnd(listBox);
            }
        }
    }

    private static void ScrollToEnd(ListBox listBox)
    {
        if (listBox.ItemCount == 0)
        {
            return;
        }

        var scrollViewer = listBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        scrollViewer?.ScrollToEnd();
    }
}

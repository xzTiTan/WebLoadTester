using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;

namespace WebLoadTester.Presentation.Common.Behaviors;

public static class AutoScrollBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<AutoScrollBehavior, Control, bool>("IsEnabled");

    static AutoScrollBehavior()
    {
        IsEnabledProperty.Changed.Subscribe(OnChanged);
    }

    public static void SetIsEnabled(AvaloniaObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(AvaloniaObject element) =>
        element.GetValue(IsEnabledProperty);

    private static void OnChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Sender is not ListBox listBox)
        {
            return;
        }

        if ((bool)args.NewValue!)
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
        listBox.ScrollIntoView(listBox.Items[listBox.ItemCount - 1]);
    }
}

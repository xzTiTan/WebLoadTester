using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;

namespace WebLoadTester.Presentation.Common.Behaviors;

public static class AutoScrollBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(AutoScrollBehavior));

    static AutoScrollBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs args)
    {
        if (control is not ListBox listBox)
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

        listBox.ScrollIntoView(listBox.ItemCount - 1);
    }
}

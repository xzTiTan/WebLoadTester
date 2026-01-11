using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;

namespace WebLoadTester.Presentation.Common.Behaviors;

/// <summary>
/// Поведение автопрокрутки ListBox при добавлении элементов.
/// </summary>
public static class AutoScrollBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(AutoScrollBehavior));

    /// <summary>
    /// Регистрирует обработчик изменения свойства IsEnabled.
    /// </summary>
    static AutoScrollBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    /// <summary>
    /// Возвращает значение свойства IsEnabled для контрола.
    /// </summary>
    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);
    /// <summary>
    /// Устанавливает значение свойства IsEnabled для контрола.
    /// </summary>
    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    /// <summary>
    /// Обрабатывает включение поведения и подписывается на изменения коллекции.
    /// </summary>
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

    /// <summary>
    /// Прокручивает список к последнему элементу.
    /// </summary>
    private static void ScrollToEnd(ListBox listBox)
    {
        if (listBox.ItemCount == 0)
        {
            return;
        }

        listBox.ScrollIntoView(listBox.ItemCount - 1);
    }
}

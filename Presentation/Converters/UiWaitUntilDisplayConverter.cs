using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.Converters;

public sealed class UiWaitUntilDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not UiWaitUntil waitUntil)
        {
            return string.Empty;
        }

        return waitUntil switch
        {
            UiWaitUntil.DomContentLoaded => "DOM готов",
            UiWaitUntil.Load => "Полная загрузка",
            UiWaitUntil.NetworkIdle => "Сеть спокойна",
            _ => waitUntil.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

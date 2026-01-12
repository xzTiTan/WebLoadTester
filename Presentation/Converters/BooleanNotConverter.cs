using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace WebLoadTester.Presentation.Converters;

public class BooleanNotConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolValue ? !boolValue : value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool boolValue ? !boolValue : value;
    }
}

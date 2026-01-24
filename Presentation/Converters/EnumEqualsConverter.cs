using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace WebLoadTester.Presentation.Converters;

public class EnumEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
        {
            return false;
        }

        var valueText = value.ToString();
        var parameterText = parameter.ToString();
        return string.Equals(valueText, parameterText, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

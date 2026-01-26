using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace WebLoadTester.Presentation.Converters;

public sealed class NullToBoolConverter : IValueConverter
{
    public static readonly NullToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value != null;
        if (parameter is string { Length: > 0 } param &&
            param.Equals("invert", StringComparison.OrdinalIgnoreCase))
        {
            return !result;
        }

        return result;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

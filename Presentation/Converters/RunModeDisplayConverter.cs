using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.Converters;

public sealed class RunModeDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RunMode mode)
        {
            return value?.ToString() ?? string.Empty;
        }

        return mode switch
        {
            RunMode.Iterations => "По числу итераций",
            RunMode.Duration => "По длительности",
            _ => mode.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}

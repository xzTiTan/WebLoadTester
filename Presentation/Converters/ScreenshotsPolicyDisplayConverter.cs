using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.Converters;

public sealed class ScreenshotsPolicyDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ScreenshotsPolicy policy)
        {
            return string.Empty;
        }

        return policy switch
        {
            ScreenshotsPolicy.Off => "Выкл",
            ScreenshotsPolicy.OnError => "При ошибке",
            ScreenshotsPolicy.Always => "Всегда",
            _ => policy.ToString()
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

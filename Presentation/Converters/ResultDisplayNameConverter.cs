using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.Converters;

public sealed class ResultDisplayNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            RunResult run => run.Name,
            CheckResult check => check.Name,
            ProbeResult probe => probe.Name,
            TimingResult timing => timing.Name,
            _ => string.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }
}

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using WebLoadTester.Presentation.ViewModels;

namespace WebLoadTester.Presentation.Converters;

public sealed class RunIdShortDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => RunsTabViewModel.FormatRunId(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value;
}

public sealed class RunStartedAtDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTimeOffset startedAt ? RunsTabViewModel.FormatStartedAt(startedAt) : "—";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value;
}

public sealed class RunDurationDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is double durationMs ? RunsTabViewModel.FormatDuration(durationMs) : "—";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value;
}

public sealed class RunStatusDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => RunsTabViewModel.MapStatus(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value;
}

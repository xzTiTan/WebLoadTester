using System;

namespace WebLoadTester.Presentation.Common;

public static class InputValueGuard
{
    public static int NormalizeInt(int value, int minInclusive, int defaultValue)
    {
        var safeDefault = Math.Max(minInclusive, defaultValue);
        return value < minInclusive ? safeDefault : value;
    }

    public static int NormalizeIntInRange(int value, int minInclusive, int maxInclusive, int defaultValue)
    {
        var boundedDefault = Math.Clamp(defaultValue, minInclusive, maxInclusive);
        if (value < minInclusive || value > maxInclusive)
        {
            return boundedDefault;
        }

        return value;
    }

    public static int? NormalizeNullableInt(int? value, int minInclusive)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value < minInclusive ? minInclusive : value.Value;
    }

    public static TEnum NormalizeEnum<TEnum>(TEnum value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return Enum.IsDefined(typeof(TEnum), value) ? value : fallback;
    }

    public static string NormalizeRequiredText(string? value, string fallback)
    {
        var normalized = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    public static string NormalizeOptionalText(string? value)
    {
        return (value ?? string.Empty).Trim();
    }
}

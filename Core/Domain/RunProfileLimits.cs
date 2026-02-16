namespace WebLoadTester.Core.Domain;

/// <summary>
/// Единые безопасные лимиты профиля запуска для MVP.
/// </summary>
public static class RunProfileLimits
{
    public const int MaxParallelism = 25;
    public const int MaxDurationSeconds = 60;
}

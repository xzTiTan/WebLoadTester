namespace WebLoadTester.Core.Domain;

/// <summary>
/// Итоговый статус выполнения прогона.
/// </summary>
public enum TestStatus
{
    Success,
    Failed,
    Partial,
    Cancelled
}

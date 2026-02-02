using System;

namespace WebLoadTester.Core.Domain;

/// <summary>
/// Исключение для немедленного остановa прогона по критической ошибке модуля.
/// </summary>
public class RunAbortException : Exception
{
    public RunAbortException(string message) : base(message)
    {
    }
}

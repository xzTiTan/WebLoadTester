namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Контракт для вывода логов разного уровня.
/// </summary>
public interface ILogSink
{
    /// <summary>
    /// Лог уровня информация.
    /// </summary>
    void Info(string message);
    /// <summary>
    /// Лог уровня предупреждение.
    /// </summary>
    void Warn(string message);
    /// <summary>
    /// Лог уровня ошибка.
    /// </summary>
    void Error(string message);
}

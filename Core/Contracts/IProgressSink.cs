namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Структура данных для передачи прогресса выполнения.
/// </summary>
public record ProgressUpdate(int Current, int Total, string Message);

/// <summary>
/// Контракт для публикации прогресса выполнения задач.
/// </summary>
public interface IProgressSink
{
    /// <summary>
    /// Сообщает о текущем прогрессе выполнения.
    /// </summary>
    void Report(ProgressUpdate update);
}

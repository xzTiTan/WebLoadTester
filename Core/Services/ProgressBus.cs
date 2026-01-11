using System;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

/// <summary>
/// Шина прогресса, транслирующая обновления подписчикам.
/// </summary>
public class ProgressBus : IProgressSink
{
    public event Action<ProgressUpdate>? ProgressChanged;

    /// <summary>
    /// Отправляет событие о прогрессе выполнения.
    /// </summary>
    public void Report(ProgressUpdate update)
    {
        ProgressChanged?.Invoke(update);
    }
}

using System;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Контекст запуска теста: доступ к логам, прогрессу и настройкам.
/// </summary>
public interface IRunContext
{
    /// <summary>
    /// Логгер для текущего запуска.
    /// </summary>
    ILogSink Log { get; }
    /// <summary>
    /// Источник прогресса выполнения.
    /// </summary>
    IProgressSink Progress { get; }
    /// <summary>
    /// Хранилище артефактов.
    /// </summary>
    IArtifactStore Artifacts { get; }
    /// <summary>
    /// Лимиты выполнения (конкурентность, RPS и т.д.).
    /// </summary>
    Limits Limits { get; }
    /// <summary>
    /// Опциональный Telegram-уведомитель.
    /// </summary>
    ITelegramNotifier? Telegram { get; }
    /// <summary>
    /// Текущее время выполнения.
    /// </summary>
    DateTimeOffset Now { get; }
}

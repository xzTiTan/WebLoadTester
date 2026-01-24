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
    /// Идентификатор прогона.
    /// </summary>
    string RunId { get; }
    /// <summary>
    /// Профиль запуска (снимок).
    /// </summary>
    RunProfile Profile { get; }
    /// <summary>
    /// Имя теста для отчётности.
    /// </summary>
    string TestName { get; }
    /// <summary>
    /// Идентификатор теста в библиотеке.
    /// </summary>
    Guid TestCaseId { get; }
    /// <summary>
    /// Версия теста в библиотеке.
    /// </summary>
    int TestCaseVersion { get; }
    /// <summary>
    /// Папка прогона с артефактами.
    /// </summary>
    string RunFolder { get; }
    /// <summary>
    /// Опциональный Telegram-уведомитель.
    /// </summary>
    ITelegramNotifier? Telegram { get; }
    /// <summary>
    /// Текущее время выполнения.
    /// </summary>
    DateTimeOffset Now { get; }
}

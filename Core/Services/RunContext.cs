using System;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services;

/// <summary>
/// Контекст запуска теста с доступом к логам, прогрессу и артефактам.
/// </summary>
public class RunContext : IRunContext
{
    /// <summary>
    /// Создаёт контекст запуска и сохраняет зависимости.
    /// </summary>
    public RunContext(ILogSink log, IProgressSink progress, IArtifactStore artifacts, Limits limits, ITelegramNotifier? telegram)
    {
        Log = log;
        Progress = progress;
        Artifacts = artifacts;
        Limits = limits;
        Telegram = telegram;
    }

    public ILogSink Log { get; }
    public IProgressSink Progress { get; }
    public IArtifactStore Artifacts { get; }
    public Limits Limits { get; }
    public ITelegramNotifier? Telegram { get; }
    /// <summary>
    /// Возвращает текущее время запуска.
    /// </summary>
    public DateTimeOffset Now => DateTimeOffset.Now;
}

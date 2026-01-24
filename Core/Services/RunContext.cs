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
    public RunContext(ILogSink log, IProgressSink progress, IArtifactStore artifacts, Limits limits, ITelegramNotifier? telegram,
        string runId, RunProfile profile, string testName, Guid testCaseId, int testCaseVersion)
    {
        Log = log;
        Progress = progress;
        Artifacts = artifacts;
        Limits = limits;
        Telegram = telegram;
        RunId = runId;
        Profile = profile;
        TestName = testName;
        TestCaseId = testCaseId;
        TestCaseVersion = testCaseVersion;
        RunFolder = string.Empty;
    }

    public ILogSink Log { get; }
    public IProgressSink Progress { get; }
    public IArtifactStore Artifacts { get; }
    public Limits Limits { get; }
    public ITelegramNotifier? Telegram { get; }
    public string RunId { get; }
    public RunProfile Profile { get; }
    public string TestName { get; }
    public Guid TestCaseId { get; }
    public int TestCaseVersion { get; }
    public string RunFolder { get; private set; }
    /// <summary>
    /// Возвращает текущее время запуска.
    /// </summary>
    public DateTimeOffset Now => DateTimeOffset.Now;

    /// <summary>
    /// Инициализирует папку прогона для артефактов.
    /// </summary>
    public void SetRunFolder(string runFolder)
    {
        RunFolder = runFolder;
    }
}

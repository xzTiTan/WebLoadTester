using System;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services;

/// <summary>
/// Контекст запуска теста с доступом к логам, прогрессу и артефактам.
/// </summary>
public class RunContext : IRunContext
{
    private readonly Func<bool> _isStopRequested;
    /// <summary>
    /// Создаёт контекст запуска и сохраняет зависимости.
    /// </summary>
    public RunContext(ILogSink log, IProgressSink progress, IArtifactStore artifacts, Limits limits, ITelegramNotifier? telegram,
        string runId, RunProfile profile, string testName, Guid testCaseId, int testCaseVersion,
        int workerId = 0, int iteration = 0, Func<bool>? isStopRequested = null)
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
        WorkerId = workerId;
        Iteration = iteration;
        _isStopRequested = isStopRequested ?? (() => false);
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
    public int WorkerId { get; }
    public int Iteration { get; }
    public bool IsStopRequested => _isStopRequested();
    /// <summary>
    /// Возвращает текущее время запуска.
    /// </summary>
    public DateTimeOffset Now => DateTimeOffset.UtcNow;

    /// <summary>
    /// Инициализирует папку прогона для артефактов.
    /// </summary>
    public void SetRunFolder(string runFolder)
    {
        RunFolder = runFolder;
    }

    public RunContext CreateScoped(int workerId, int iteration)
    {
        var scoped = new RunContext(Log, Progress, Artifacts, Limits, Telegram, RunId, Profile, TestName, TestCaseId, TestCaseVersion, workerId, iteration, _isStopRequested);
        scoped.SetRunFolder(RunFolder);
        return scoped;
    }
}

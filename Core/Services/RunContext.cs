using System;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services;

public sealed class RunContext : IRunContext
{
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
    public DateTimeOffset Now => DateTimeOffset.Now;
}

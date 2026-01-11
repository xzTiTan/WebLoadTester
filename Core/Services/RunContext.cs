using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services;

public sealed class RunContext : IRunContext
{
    public required ILogSink Log { get; init; }
    public required IProgressSink Progress { get; init; }
    public required IArtifactStore Artifacts { get; init; }
    public required Limits Limits { get; init; }
    public ITelegramNotifier? Telegram { get; init; }
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}

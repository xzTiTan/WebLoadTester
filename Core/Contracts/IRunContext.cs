using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

public interface IRunContext
{
    ILogSink Log { get; }
    IProgressSink Progress { get; }
    IArtifactStore Artifacts { get; }
    Limits Limits { get; }
    ITelegramNotifier? Telegram { get; }
    DateTimeOffset Now { get; }
}

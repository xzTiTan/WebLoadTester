using WebLoadTester.Domain;

namespace WebLoadTester.Services.Strategies;

public class ScreenshotRunStrategy : BaseRunStrategy, IRunStrategy
{
    public Task<List<RunResult>> ExecuteAsync(RunContext context, CancellationToken ct)
    {
        var runs = Math.Max(1, context.Settings.TotalRuns);
        context.Logger.Log($"[Screenshot] {runs} скриншотов");
        return RunWithQueueAsync(context, runs, Math.Max(1, context.Settings.Concurrency), ct);
    }
}

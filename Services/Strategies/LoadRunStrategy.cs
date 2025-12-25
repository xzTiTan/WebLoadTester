using WebLoadTester.Domain;

namespace WebLoadTester.Services.Strategies;

public class LoadRunStrategy : BaseRunStrategy, IRunStrategy
{
    public Task<List<RunResult>> ExecuteAsync(RunContext context, CancellationToken ct)
    {
        var runs = Math.Max(1, context.Settings.TotalRuns);
        var concurrency = Math.Clamp(context.Settings.Concurrency, 1, 50);
        context.Logger.Log($"[Load] Запуск {runs} прогонов при параллельности {concurrency}");
        return RunWithQueueAsync(context, runs, concurrency, ct);
    }
}

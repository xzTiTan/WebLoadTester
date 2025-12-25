using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain;

namespace WebLoadTester.Services.Strategies;

public class E2ERunStrategy : BaseRunStrategy, IRunStrategy
{
    public Task<List<RunResult>> ExecuteAsync(RunContext context, CancellationToken ct)
    {
        context.Logger.Log("[E2E] Старт последовательного сценария");
        var concurrency = Math.Max(1, context.Settings.Concurrency);
        return RunWithQueueAsync(context, context.Settings.TotalRuns, concurrency, ct);
    }
}

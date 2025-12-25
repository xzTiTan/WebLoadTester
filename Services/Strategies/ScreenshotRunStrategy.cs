using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain;

namespace WebLoadTester.Services.Strategies
{
    public class ScreenshotRunStrategy : BaseRunStrategy, IRunStrategy
    {
        public Task<List<RunResult>> ExecuteAsync(RunContext context, CancellationToken ct)
        {
            var runs = Math.Max(1, context.Settings.TotalRuns);
            var concurrency = Math.Max(1, context.Settings.Concurrency);
            context.Logger.Log($"[Screenshot] {runs} скриншотов (conc={concurrency})");
            return RunWithQueueAsync(context, runs, concurrency, ct);
        }
    }
}

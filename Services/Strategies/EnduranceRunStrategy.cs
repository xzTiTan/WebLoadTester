using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain;

namespace WebLoadTester.Services.Strategies
{
    public class EnduranceRunStrategy : IRunStrategy
    {
        public async Task<List<RunResult>> ExecuteAsync(RunContext context, CancellationToken ct)
        {
            var results = new List<RunResult>();
            var endAt = DateTime.UtcNow.AddMinutes(Math.Max(1, context.Settings.Endurance.DurationMinutes));
            var concurrency = Math.Max(1, context.Settings.Concurrency);
            var runId = 0;
            var tasks = new List<Task>();

            for (var worker = 1; worker <= concurrency; worker++)
            {
                var id = worker;
                tasks.Add(Task.Run(async () =>
                {
                    while (DateTime.UtcNow < endAt)
                    {
                        ct.ThrowIfCancellationRequested();
                        var currentRun = Interlocked.Increment(ref runId);
                        var res = await context.Runner.RunOnceAsync(context.Scenario, context.Settings, id, currentRun, context.Logger, ct);
                        lock (results)
                        {
                            results.Add(res);
                        }

                        context.Progress?.Invoke(currentRun, 0);
                    }
                }, ct));
            }

            await Task.WhenAll(tasks);
            return results;
        }
    }
}

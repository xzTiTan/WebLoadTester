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
            var minutes = Math.Max(1, context.Settings.EnduranceMinutes);
            if (context.Settings.EnduranceMinutes <= 0)
            {
                context.Logger.Log("[Endurance] Duration is 0; using 1 minute by default");
            }

            var endAt = DateTime.UtcNow.AddMinutes(minutes);
            var concurrency = Math.Clamp(context.Settings.Concurrency, 1, 50);
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
                        var res = await context.Runner.RunOnceAsync(new RunRequest
                        {
                            Scenario = context.Scenario,
                            Settings = context.Settings,
                            WorkerId = id,
                            RunId = currentRun,
                            Logger = context.Logger,
                            CancelAll = context.Cancellation,
                            PhaseName = "Endurance"
                        }, ct);
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

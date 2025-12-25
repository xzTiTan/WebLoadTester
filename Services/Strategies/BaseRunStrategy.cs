using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WebLoadTester.Domain;

namespace WebLoadTester.Services.Strategies
{
    public abstract class BaseRunStrategy
    {
        protected async Task<List<RunResult>> RunWithQueueAsync(
            RunContext context,
            int totalRuns,
            int concurrency,
            CancellationToken ct,
            Action<int>? onRunCompleted = null,
            int? totalForProgress = null)
        {
            var results = new List<RunResult>();
            var runsChannel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false
            });

            for (var i = 1; i <= totalRuns; i++)
            {
                runsChannel.Writer.TryWrite(i);
            }
            runsChannel.Writer.Complete();

            var tasks = new List<Task>();
            int completed = 0;
            var progressTotal = totalForProgress ?? totalRuns;
            for (var workerId = 1; workerId <= concurrency; workerId++)
            {
                var id = workerId;
                tasks.Add(Task.Run(async () =>
                {
                    await foreach (var runId in runsChannel.Reader.ReadAllAsync(ct))
                    {
                        var res = await context.Runner.RunOnceAsync(context.Scenario, context.Settings, id, runId, context.Logger, ct, context.Cancellation);
                        lock (results)
                        {
                            results.Add(res);
                        }

                        var done = Interlocked.Increment(ref completed);
                        if (onRunCompleted is not null)
                        {
                            onRunCompleted(done);
                        }
                        else
                        {
                            context.Progress?.Invoke(done, progressTotal);
                        }
                    }
                }, ct));
            }

            await Task.WhenAll(tasks);
            return results;
        }
    }
}

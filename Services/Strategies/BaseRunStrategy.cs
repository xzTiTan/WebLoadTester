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
                        var res = await context.Runner.RunOnceAsync(new RunRequest
                        {
                            Scenario = context.Scenario,
                            Settings = context.Settings,
                            WorkerId = id,
                            RunId = runId,
                            Logger = context.Logger,
                            CancelAll = context.Cancellation,
                            PhaseName = ""
                        }, ct);
                        lock (results)
                        {
                            results.Add(res);
                        }

                        if (res.StopAllRequested)
                        {
                            context.Cancellation?.Cancel();
                            break;
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

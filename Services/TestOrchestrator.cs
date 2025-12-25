using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WebLoadTester.Domain;
using WebLoadTester.Reports;

namespace WebLoadTester.Services;

public class TestOrchestrator
{
    private int _globalRunId;
    private readonly ReportWriter _reportWriter = new();

    public async Task<TestRunResult> ExecuteAsync(RunContext context, TestPlan plan, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var totalRuns = plan.TotalRuns;
        var results = new List<RunResult>();
        _globalRunId = 0;

        foreach (var phase in plan.Phases)
        {
            ct.ThrowIfCancellationRequested();
            context.Logger.Log($"Фаза: {phase.Name}, Concurrency={phase.Concurrency}, Runs={(phase.Runs?.ToString() ?? "∞")}, Duration={(phase.Duration?.ToString() ?? "—")}, PauseAfter={phase.PauseAfterSeconds}s");
            var phaseResults = await RunPhaseAsync(phase, context, totalRuns, ct);
            results.AddRange(phaseResults);

            if (phase.PauseAfterSeconds > 0)
            {
                context.Logger.Log($"Пауза между фазами {phase.PauseAfterSeconds} сек.");
                await Task.Delay(TimeSpan.FromSeconds(phase.PauseAfterSeconds), ct);
            }
        }

        var finished = DateTime.UtcNow;
        var reportPath = await _reportWriter.WriteAsync(context.Settings, results, started, finished, ct);

        return new TestRunResult
        {
            Runs = results,
            ReportPath = reportPath,
            StartedAt = started,
            FinishedAt = finished
        };
    }

    private async Task<List<RunResult>> RunPhaseAsync(TestPhase phase, RunContext context, int totalRuns, CancellationToken ct)
    {
        var results = new List<RunResult>();
        var runsChannel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
        var writerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        Task writer;
        if (phase.Duration.HasValue)
        {
            writer = Task.Run(async () =>
            {
                var end = DateTime.UtcNow.Add(phase.Duration.Value);
                while (!writerCts.IsCancellationRequested && DateTime.UtcNow < end)
                {
                    var next = Interlocked.Increment(ref _globalRunId);
                    await runsChannel.Writer.WriteAsync(next, writerCts.Token);
                }

                runsChannel.Writer.TryComplete();
            }, writerCts.Token);
        }
        else
        {
            writer = Task.Run(() =>
            {
                var runs = phase.Runs ?? 0;
                for (var i = 0; i < runs; i++)
                {
                    var next = Interlocked.Increment(ref _globalRunId);
                    runsChannel.Writer.TryWrite(next);
                }

                runsChannel.Writer.TryComplete();
            }, writerCts.Token);
        }

        var completed = 0;
        var progressTotal = totalRuns;
        var workerTasks = Enumerable.Range(1, Math.Max(1, phase.Concurrency)).Select(workerId => Task.Run(async () =>
        {
            await foreach (var runId in runsChannel.Reader.ReadAllAsync(ct))
            {
                var res = await context.Runner.RunOnceAsync(context.Scenario, context.Settings, workerId, runId, context.Logger, ct, context.Cancellation);
                lock (results)
                {
                    results.Add(res);
                }

                var done = Interlocked.Increment(ref completed);
                context.Progress?.Invoke(done, progressTotal);
            }
        }, ct)).ToList();

        try
        {
            await Task.WhenAll(workerTasks);
        }
        finally
        {
            writerCts.Cancel();
            try
            {
                await writer;
            }
            catch (OperationCanceledException)
            {
            }
        }

        context.Logger.Log($"Фаза {phase.Name} завершена: {results.Count} прогонов");
        return results;
    }
}

public class TestRunResult
{
    public List<RunResult> Runs { get; set; } = new();
    public string ReportPath { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
}

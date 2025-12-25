using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WebLoadTester.Domain;
using WebLoadTester.Domain.Reporting;
using WebLoadTester.Reports;
using WebLoadTester.Services.Reporting;

namespace WebLoadTester.Services;

public class TestOrchestrator
{
    private int _globalRunId;
    private readonly JsonReportWriter _jsonReportWriter = new();
    private readonly HtmlReportWriter _htmlReportWriter = new();

    public async Task<TestRunResult> ExecuteAsync(RunContext context, TestPlan plan, CancellationToken ct)
    {
        PlaywrightBootstrap.EnsureBrowsersPathAndReturn(AppContext.BaseDirectory);
        var started = DateTime.UtcNow;
        var totalRuns = plan.TotalRuns;
        var results = new List<RunResult>();
        _globalRunId = 0;
        var status = "Completed";
        string? reportPath = null;
        string? htmlPath = null;
        var finished = started;

        try
        {
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
        }
        catch (OperationCanceledException)
        {
            status = "Stopped";
            context.Logger.Log("Тест остановлен пользователем или политикой.");
        }
        catch (Exception ex)
        {
            status = "Error";
            context.Logger.Log($"Ошибка выполнения теста: {ex.Message}");
        }
        finally
        {
            finished = DateTime.UtcNow;
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var selectors = context.Scenario.Steps.ConvertAll(s => s.Selector);
            var report = TestMetricsCalculator.BuildReport(context.Settings, selectors, plan.Phases, results, started, finished, status, totalRuns);

            try
            {
                reportPath = await _jsonReportWriter.WriteAsync(report, timestamp, CancellationToken.None);
                context.Logger.Log($"Report saved: {reportPath}");
            }
            catch (Exception ex)
            {
                context.Logger.Log($"Не удалось сохранить JSON отчёт: {ex.Message}");
            }

            try
            {
                htmlPath = Path.Combine("reports", $"report_{timestamp}.html");
                await _htmlReportWriter.WriteAsync(report, htmlPath, CancellationToken.None);
                context.Logger.Log($"HTML report saved: {htmlPath}");
            }
            catch (Exception ex)
            {
                context.Logger.Log($"Не удалось сохранить HTML отчёт: {ex.Message}");
            }
        }

        return new TestRunResult
        {
            Runs = results,
            ReportPath = reportPath ?? string.Empty,
            HtmlReportPath = htmlPath ?? string.Empty,
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
                    await Task.Delay(10, writerCts.Token);
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
                var res = await context.Runner.RunOnceAsync(new RunRequest
                {
                    Scenario = context.Scenario,
                    Settings = context.Settings,
                    WorkerId = workerId,
                    RunId = runId,
                    Logger = context.Logger,
                    CancelAll = context.Cancellation,
                    PhaseName = phase.Name
                }, ct);
                res.PhaseName = phase.Name;
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
    public string HtmlReportPath { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Core.Services.ReportWriters;
using WebLoadTester.Infrastructure.Storage;
using Xunit;

namespace WebLoadTester.Tests;

public class OrchestratorSemanticsTests
{
    [Fact]
    public async Task PauseBetweenIterationsMs_DelaysIterations_AndPersistsWorkerMetadata()
    {
        var harness = await CreateHarnessAsync(new RunProfile
        {
            Mode = RunMode.Iterations,
            Iterations = 3,
            Parallelism = 1,
            TimeoutSeconds = 5,
            PauseBetweenIterationsMs = 200
        });

        var seen = new ConcurrentBag<(int WorkerId, int Iteration)>();
        var module = new FakeSemanticsModule(async (ctx, _) =>
        {
            seen.Add((ctx.WorkerId, ctx.Iteration));
            await Task.Yield();
            return SuccessResult($"iter-{ctx.Iteration}");
        });

        var sw = Stopwatch.StartNew();
        var report = await harness.Orchestrator.StartAsync(module, new FakeSettings(), harness.Context, CancellationToken.None);
        sw.Stop();

        Assert.Equal(TimeSpan.Zero, report.StartedAt.Offset);
        Assert.Equal(TimeSpan.Zero, report.FinishedAt.Offset);
        Assert.Equal(TestStatus.Success, report.Status);
        Assert.Equal(3, seen.Count);
        Assert.All(seen, item =>
        {
            Assert.True(item.WorkerId > 0);
            Assert.True(item.Iteration > 0);
        });
        Assert.True(sw.ElapsedMilliseconds >= 300, $"Expected pause to affect runtime, actual: {sw.ElapsedMilliseconds}ms");

        var detail = await harness.Store.GetRunDetailAsync(report.RunId, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Equal(3, detail!.Items.Count);
        Assert.All(detail.Items, item =>
        {
            Assert.True(item.WorkerId > 0);
            Assert.True(item.Iteration > 0);
        });
    }

    [Fact]
    public async Task StopRequested_CompletesCurrentIteration_AndReturnsStopped()
    {
        var stopRequested = 0;
        var harness = await CreateHarnessAsync(new RunProfile
        {
            Mode = RunMode.Iterations,
            Iterations = 10,
            Parallelism = 1,
            TimeoutSeconds = 5,
            PauseBetweenIterationsMs = 0
        }, () => Volatile.Read(ref stopRequested) == 1);

        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var iterations = new ConcurrentBag<int>();

        var module = new FakeSemanticsModule(async (ctx, ct) =>
        {
            iterations.Add(ctx.Iteration);
            started.TrySetResult(true);
            await Task.Delay(120, ct);
            return SuccessResult($"iter-{ctx.Iteration}");
        });

        var runTask = harness.Orchestrator.StartAsync(module, new FakeSettings(), harness.Context, CancellationToken.None);
        await started.Task;
        Interlocked.Exchange(ref stopRequested, 1);

        var report = await runTask;
        Assert.Equal(TestStatus.Stopped, report.Status);
        Assert.Single(iterations);
    }

    [Fact]
    public async Task Cancel_StopsImmediately_AndReturnsCanceled()
    {
        var harness = await CreateHarnessAsync(new RunProfile
        {
            Mode = RunMode.Iterations,
            Iterations = 10,
            Parallelism = 1,
            TimeoutSeconds = 30
        });

        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new FakeSemanticsModule(async (_, ct) =>
        {
            started.TrySetResult(true);
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return SuccessResult("cancel-test");
        });

        using var cts = new CancellationTokenSource();
        var runTask = harness.Orchestrator.StartAsync(module, new FakeSettings(), harness.Context, cts.Token);
        await started.Task;
        cts.Cancel();

        var report = await runTask;
        Assert.Equal(TestStatus.Canceled, report.Status);
    }


    [Fact]
    public async Task MixedResults_ReturnsFailed_NotPartial()
    {
        var harness = await CreateHarnessAsync(new RunProfile
        {
            Mode = RunMode.Iterations,
            Iterations = 2,
            Parallelism = 1,
            TimeoutSeconds = 5
        });

        var module = new FakeSemanticsModule((ctx, _) => Task.FromResult(new ModuleResult
        {
            Status = TestStatus.Success,
            Results = new List<ResultBase>
            {
                new CheckResult($"iter-{ctx.Iteration}")
                {
                    Success = ctx.Iteration == 1,
                    DurationMs = 1,
                    ErrorMessage = ctx.Iteration == 1 ? null : "boom"
                }
            }
        }));

        var report = await harness.Orchestrator.StartAsync(module, new FakeSettings(), harness.Context, CancellationToken.None);
        Assert.Equal(TestStatus.Failed, report.Status);
    }

    [Fact]
    public async Task StopRequested_WithAnyFailure_ReturnsFailed()
    {
        var stopRequested = 0;
        var harness = await CreateHarnessAsync(new RunProfile
        {
            Mode = RunMode.Iterations,
            Iterations = 10,
            Parallelism = 1,
            TimeoutSeconds = 5
        }, () => Volatile.Read(ref stopRequested) == 1);

        var module = new FakeSemanticsModule(async (ctx, ct) =>
        {
            if (ctx.Iteration == 1)
            {
                Interlocked.Exchange(ref stopRequested, 1);
                return new ModuleResult
                {
                    Status = TestStatus.Failed,
                    Results = new List<ResultBase>
                    {
                        new CheckResult("failed-first")
                        {
                            Success = false,
                            DurationMs = 1,
                            ErrorMessage = "stop-after-fail"
                        }
                    }
                };
            }

            await Task.Delay(20, ct);
            return SuccessResult($"iter-{ctx.Iteration}");
        });

        var report = await harness.Orchestrator.StartAsync(module, new FakeSettings(), harness.Context, CancellationToken.None);
        Assert.Equal(TestStatus.Failed, report.Status);
    }
    [Fact]
    public async Task FailedIteration_ReturnsFailedStatus()
    {
        var harness = await CreateHarnessAsync(new RunProfile
        {
            Mode = RunMode.Iterations,
            Iterations = 1,
            Parallelism = 1,
            TimeoutSeconds = 5
        });

        var module = new FakeSemanticsModule((ctx, _) => Task.FromResult(new ModuleResult
        {
            Status = TestStatus.Failed,
            Results = new List<ResultBase>
            {
                new CheckResult($"iter-{ctx.Iteration}")
                {
                    Success = false,
                    DurationMs = 1,
                    ErrorMessage = "boom"
                }
            }
        }));

        var report = await harness.Orchestrator.StartAsync(module, new FakeSettings(), harness.Context, CancellationToken.None);
        Assert.Equal(TestStatus.Failed, report.Status);
    }

    private static async Task<TestHarness> CreateHarnessAsync(RunProfile profile, Func<bool>? stopRequested = null)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WebLoadTesterTests", Guid.NewGuid().ToString("N"));
        var runsRoot = Path.Combine(tempRoot, "runs");
        var profilesRoot = Path.Combine(tempRoot, "profiles");
        var dbPath = Path.Combine(tempRoot, "data", "runs.db");

        var store = new SqliteRunStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);

        var artifactStore = new ArtifactStore(runsRoot, profilesRoot);
        var context = new RunContext(new LogBus(), new ProgressBus(), artifactStore, new Limits(), null,
            Guid.NewGuid().ToString("N"), profile, "TestFinalName", Guid.NewGuid(), 1, isStopRequested: stopRequested);

        var orchestrator = new RunOrchestrator(new JsonReportWriter(artifactStore), new HtmlReportWriter(artifactStore), store);

        return new TestHarness(store, context, orchestrator);
    }

    private static ModuleResult SuccessResult(string name)
    {
        return new ModuleResult
        {
            Status = TestStatus.Success,
            Results = new List<ResultBase>
            {
                new CheckResult(name)
                {
                    Success = true,
                    DurationMs = 1
                }
            }
        };
    }

    private sealed record TestHarness(SqliteRunStore Store, RunContext Context, RunOrchestrator Orchestrator);

    private sealed class FakeSettings;

    private sealed class FakeSemanticsModule(Func<IRunContext, CancellationToken, Task<ModuleResult>> execute) : ITestModule
    {
        public string Id => "test.fake";
        public string DisplayName => "Fake module";
        public string Description => "Fake module for orchestrator tests";
        public TestFamily Family => TestFamily.HttpTesting;
        public Type SettingsType => typeof(FakeSettings);
        public object CreateDefaultSettings() => new FakeSettings();
        public IReadOnlyList<string> Validate(object settings) => Array.Empty<string>();
        public Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct) => execute(ctx, ct);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Presentation.ViewModels;
using Xunit;

namespace WebLoadTester.Tests;

public class RunsTabTests
{
    [Fact]
    public void ParseRepeatSnapshot_ExtractsModuleProfileAndSettings()
    {
        var reportJson = """
                         {
                           "moduleId": "http.functional",
                           "profile": {
                             "parallelism": 3,
                             "mode": "iterations",
                             "iterations": 5,
                             "durationSeconds": 120,
                             "pauseBetweenIterationsMs": 250,
                             "timeouts": { "operationSeconds": 15 },
                             "headless": true,
                             "screenshotsPolicy": "OnError",
                             "htmlReportEnabled": false,
                             "telegramEnabled": false,
                             "preflightEnabled": true
                           },
                           "moduleSettings": {
                             "baseUrl": "https://example.com"
                           }
                         }
                         """;

        var ok = RunsTabViewModel.TryParseRepeatSnapshot(reportJson, out var snapshot, out var error);

        Assert.True(ok, error);
        Assert.Equal("http.functional", snapshot.ModuleId);
        Assert.Equal(3, snapshot.Profile.Parallelism);
        Assert.Equal(RunMode.Iterations, snapshot.Profile.Mode);
        Assert.Equal(15, snapshot.Profile.TimeoutSeconds);
        Assert.Equal("https://example.com", snapshot.ModuleSettings.GetProperty("baseUrl").GetString());
    }


    [Fact]
    public void ParseRepeatSnapshot_SupportsLegacyTimeoutSecondsField()
    {
        var reportJson = """
                         {
                           "moduleId": "net.availability",
                           "profile": {
                             "mode": "iterations",
                             "parallelism": 1,
                             "timeoutSeconds": 22
                           },
                           "moduleSettings": {}
                         }
                         """;

        var ok = RunsTabViewModel.TryParseRepeatSnapshot(reportJson, out var snapshot, out var error);

        Assert.True(ok, error);
        Assert.Equal(22, snapshot.Profile.TimeoutSeconds);
    }

    [Fact]
    public async Task Filters_ByPeriodStatusAndOnlyErrors()
    {
        var now = DateTimeOffset.UtcNow;
        var runs = new List<TestRunSummary>
        {
            new() { RunId = "r1", ModuleType = "net.diagnostics", Status = "Failed", FailedItems = 1, StartedAt = now.AddDays(-1), TestName = "A" },
            new() { RunId = "r2", ModuleType = "net.diagnostics", Status = "Success", FailedItems = 0, StartedAt = now.AddDays(-10), TestName = "B" },
            new() { RunId = "r3", ModuleType = "http.functional", Status = "Failed", FailedItems = 3, StartedAt = now.AddDays(-2), TestName = "C" }
        };

        var store = new FakeRunStore(runs);
        var vm = new RunsTabViewModel(store, Path.GetTempPath(), _ => Task.CompletedTask);
        vm.SetModuleOptions(new[] { "net.diagnostics", "http.functional" });

        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedModuleType = "net.diagnostics";
        vm.SelectedStatus = "Failed";
        vm.SelectedPeriod = "7 дней";
        vm.OnlyWithErrors = true;

        Assert.Single(vm.Runs);
        Assert.Equal("r1", vm.Runs[0].RunId);
    }

    [Fact]
    public async Task DeleteRun_RemovesDbRecordAndRunFolder()
    {
        var runId = "run-delete";
        var runsRoot = Path.Combine(Path.GetTempPath(), "WebLoadTesterTests", Guid.NewGuid().ToString("N"));
        var runFolder = Path.Combine(runsRoot, runId);
        Directory.CreateDirectory(runFolder);

        var store = new FakeRunStore(new List<TestRunSummary>
        {
            new() { RunId = runId, ModuleType = "net.security", Status = "Success", StartedAt = DateTimeOffset.UtcNow, TestName = "Case" }
        });

        var vm = new RunsTabViewModel(store, runsRoot, _ => Task.CompletedTask);
        await vm.RefreshCommand.ExecuteAsync(null);
        vm.SelectedRun = vm.Runs.First();

        vm.RequestDeleteRunCommand.Execute(null);
        await vm.ConfirmDeleteRunCommand.ExecuteAsync(null);

        Assert.Equal(runId, store.LastDeletedRunId);
        Assert.False(Directory.Exists(runFolder));
    }

    private sealed class FakeRunStore : IRunStore
    {
        private readonly List<TestRunSummary> _runs;
        public string? LastDeletedRunId { get; private set; }

        public FakeRunStore(List<TestRunSummary> runs)
        {
            _runs = runs;
        }

        public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<TestCase>> GetTestCasesAsync(string moduleType, CancellationToken ct) => throw new NotSupportedException();
        public Task<TestCaseVersion?> GetTestCaseVersionAsync(Guid testCaseId, int version, CancellationToken ct) => throw new NotSupportedException();
        public Task<TestCase> SaveTestCaseAsync(string name, string description, string moduleType, string payloadJson, string changeNote, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteTestCaseAsync(Guid testCaseId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RunProfile>> GetRunProfilesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<RunProfile> SaveRunProfileAsync(RunProfile profile, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteRunProfileAsync(Guid profileId, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateRunAsync(TestRun run, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateRunAsync(TestRun run, CancellationToken ct) => throw new NotSupportedException();
        public Task AddRunItemsAsync(IEnumerable<RunItem> items, CancellationToken ct) => throw new NotSupportedException();
        public Task AddArtifactsAsync(IEnumerable<ArtifactRecord> artifacts, CancellationToken ct) => throw new NotSupportedException();
        public Task AddTelegramNotificationAsync(TelegramNotification notification, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<TestRunSummary>> QueryRunsAsync(RunQuery query, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<TestRunSummary>>(_runs.ToList());
        }

        public Task<TestRunDetail?> GetRunDetailAsync(string runId, CancellationToken ct)
        {
            var run = _runs.FirstOrDefault(r => r.RunId == runId);
            if (run == null)
            {
                return Task.FromResult<TestRunDetail?>(null);
            }

            return Task.FromResult<TestRunDetail?>(new TestRunDetail
            {
                Run = new TestRun
                {
                    RunId = run.RunId,
                    Status = run.Status,
                    ModuleType = run.ModuleType,
                    ProfileSnapshotJson = JsonSerializer.Serialize(new
                    {
                        mode = "iterations",
                        parallelism = 1,
                        timeoutSeconds = 30,
                        pauseBetweenIterationsMs = 0
                    })
                },
                Items = new List<RunItem>()
            });
        }

        public Task DeleteRunAsync(string runId, CancellationToken ct)
        {
            LastDeletedRunId = runId;
            _runs.RemoveAll(r => r.RunId == runId);
            return Task.CompletedTask;
        }
    }
}

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Storage;
using Xunit;

namespace WebLoadTester.Tests;

public class StorageTests
{
    [Fact]
    public async Task InitializeAndPersistRunAsync()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WebLoadTesterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var dbPath = Path.Combine(tempRoot, "data", "runs.db");

        var store = new SqliteRunStore(dbPath);
        await store.InitializeAsync(CancellationToken.None);

        var runId = Guid.NewGuid().ToString("N");
        var testRun = new TestRun
        {
            RunId = runId,
            TestCaseId = Guid.NewGuid(),
            TestCaseVersion = 1,
            TestName = "Storage smoke",
            ModuleType = "http.functional",
            ModuleName = "HTTP",
            ProfileSnapshotJson = "{}",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "Running"
        };

        await store.CreateRunAsync(testRun, CancellationToken.None);
        await store.AddRunItemsAsync(new[]
        {
            new RunItem
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                ItemType = "Check",
                ItemKey = "Ping",
                Status = "Success",
                DurationMs = 12.5
            }
        }, CancellationToken.None);
        await store.AddArtifactsAsync(new[]
        {
            new ArtifactRecord
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                ArtifactType = "JsonReport",
                RelativePath = "report.json",
                CreatedAt = DateTimeOffset.UtcNow
            }
        }, CancellationToken.None);

        testRun.Status = "Success";
        testRun.FinishedAt = DateTimeOffset.UtcNow;
        testRun.SummaryJson = "{\"totalItems\":1}";
        await store.UpdateRunAsync(testRun, CancellationToken.None);

        var detail = await store.GetRunDetailAsync(runId, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Single(detail!.Items);
        Assert.Single(detail.Artifacts);
    }
}

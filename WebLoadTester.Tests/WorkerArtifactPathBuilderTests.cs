using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using Xunit;

namespace WebLoadTester.Tests;

public class WorkerArtifactPathBuilderTests
{
    [Fact]
    public void BuildsWorkerScopedPaths()
    {
        var root = "/tmp/run-1";

        var screenshotsDir = WorkerArtifactPathBuilder.GetWorkerScreenshotsDir(root, 2, 5);
        var profilesDir = WorkerArtifactPathBuilder.GetWorkerProfilesDir(root, 2);

        Assert.EndsWith(Path.Combine("screenshots", "w2", "it5"), screenshotsDir);
        Assert.EndsWith(Path.Combine("profiles", "w2"), profilesDir);
        Assert.Equal(Path.Combine("screenshots", "w2", "it5", "shot.png"), WorkerArtifactPathBuilder.GetWorkerScreenshotArtifactRelativePath(2, 5, "shot.png"));
    }

    [Fact]
    public async Task CreatesAndRegistersWorkerProfileSnapshots()
    {
        var runDir = Path.Combine(Path.GetTempPath(), "wlt-artifacts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDir);
        try
        {
            var ctx = new FakeRunContext(runDir, workerId: 2, iteration: 5);
            var artifacts = await WorkerArtifactPathBuilder.EnsureWorkerProfileSnapshotsAsync(ctx, new { BaseUrl = "https://example.com" }, CancellationToken.None);

            Assert.Contains(artifacts, a => a.RelativePath == Path.Combine("profiles", "w2", "profile.json"));
            Assert.Contains(artifacts, a => a.RelativePath == Path.Combine("profiles", "w2", "moduleSettings.json"));

            Assert.True(File.Exists(Path.Combine(runDir, "profiles", "w2", "profile.json")));
            Assert.True(File.Exists(Path.Combine(runDir, "profiles", "w2", "moduleSettings.json")));
        }
        finally
        {
            Directory.Delete(runDir, recursive: true);
        }
    }

    private sealed class FakeRunContext : IRunContext
    {
        public FakeRunContext(string runFolder, int workerId, int iteration)
        {
            RunFolder = runFolder;
            WorkerId = workerId;
            Iteration = iteration;
            Profile = new RunProfile { Parallelism = 2, Iterations = 3, Mode = RunMode.Iterations, TimeoutSeconds = 30 };
        }

        public ILogSink Log => throw new NotImplementedException();
        public IProgressSink Progress => throw new NotImplementedException();
        public IArtifactStore Artifacts => throw new NotImplementedException();
        public Limits Limits => new();
        public string RunId => "run";
        public RunProfile Profile { get; }
        public string TestName => "test";
        public Guid TestCaseId => Guid.Empty;
        public int TestCaseVersion => 1;
        public string RunFolder { get; }
        public ITelegramNotifier? Telegram => null;
        public int WorkerId { get; }
        public int Iteration { get; }
        public bool IsStopRequested => false;
        public DateTimeOffset Now => DateTimeOffset.UtcNow;
    }
}

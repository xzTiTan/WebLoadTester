using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services.Metrics;
using WebLoadTester.Core.Services.ReportWriters;
using WebLoadTester.Infrastructure.Storage;
using Xunit;

namespace WebLoadTester.Tests;

public class ReportingTests
{
    [Fact]
    public async Task WritesJsonReportAsync()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WebLoadTesterTests", Guid.NewGuid().ToString("N"));
        var runsRoot = Path.Combine(tempRoot, "runs");
        var profilesRoot = Path.Combine(tempRoot, "profiles");
        var store = new ArtifactStore(runsRoot, profilesRoot);
        var writer = new JsonReportWriter(store);

        var report = new TestReport
        {
            RunId = Guid.NewGuid().ToString("N"),
            FinalName = "DemoConfig_HTTPФункциональные",
            TestCaseId = Guid.NewGuid(),
            TestCaseVersion = 1,
            TestName = "Reporting smoke",
            ModuleId = "http.functional",
            ModuleName = "HTTP функциональные проверки",
            Family = TestFamily.HttpTesting,
            StartedAt = DateTimeOffset.UtcNow,
            FinishedAt = DateTimeOffset.UtcNow,
            Status = TestStatus.Success,
            AppVersion = "1.0.0",
            OsDescription = "Test OS",
            SettingsSnapshot = "{}",
            ModuleSettingsSnapshot = JsonSerializer.SerializeToElement(new
            {
                baseUrl = "https://example.com",
                timeoutSeconds = 15
            }),
            ProfileSnapshot = new RunProfile
            {
                Parallelism = 2,
                Mode = RunMode.Iterations,
                Iterations = 3,
                TimeoutSeconds = 20
            }
        };

        report.Results.Add(new EndpointResult("Ping")
        {
            Success = true,
            DurationMs = 12,
            WorkerId = 1,
            IterationIndex = 2,
            ItemIndex = 0,
            StatusCode = 200,
            LatencyMs = 12
        });
        report.Metrics = MetricsCalculator.Calculate(report.Results);

        var relative = await writer.WriteAsync(report, report.RunId);
        var jsonPath = Path.Combine(runsRoot, report.RunId, relative);

        Assert.True(File.Exists(jsonPath));
        await using var stream = File.OpenRead(jsonPath);
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;

        Assert.Equal(report.RunId, root.GetProperty("runId").GetString());
        Assert.Equal(report.ModuleId, root.GetProperty("moduleId").GetString());
        Assert.Equal(report.FinalName, root.GetProperty("finalName").GetString());

        Assert.True(root.TryGetProperty("profile", out var profile));
        Assert.Equal(report.ProfileSnapshot.Parallelism, profile.GetProperty("parallelism").GetInt32());

        Assert.True(root.TryGetProperty("moduleSettings", out var moduleSettings));
        Assert.Equal(JsonValueKind.Object, moduleSettings.ValueKind);
        Assert.Equal("https://example.com", moduleSettings.GetProperty("baseUrl").GetString());

        var item = root.GetProperty("items")[0];
        Assert.Equal("Endpoint", item.GetProperty("kind").GetString());
        Assert.Equal(1, item.GetProperty("workerId").GetInt32());
        Assert.Equal(2, item.GetProperty("iteration").GetInt32());
        Assert.Equal(0, item.GetProperty("itemIndex").GetInt32());
        Assert.Equal("Ping", item.GetProperty("name").GetString());
    }

    [Fact]
    public void OmitsHighPercentilesWhenSampleSmall()
    {
        var results = Enumerable.Range(1, 10)
            .Select(i => new CheckResult($"Check {i}")
            {
                Success = true,
                DurationMs = i
            });

        var metrics = MetricsCalculator.Calculate(results);

        Assert.Equal(0, metrics.P95Ms);
        Assert.Equal(0, metrics.P99Ms);
        Assert.Equal(10, metrics.TotalItems);
    }
}

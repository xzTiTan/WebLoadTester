using System;
using System.IO;
using System.Linq;
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
            ProfileSnapshot = new RunProfile()
        };

        report.Results.Add(new CheckResult("Ping")
        {
            Success = true,
            DurationMs = 12
        });
        report.Metrics = MetricsCalculator.Calculate(report.Results);

        var relative = await writer.WriteAsync(report, report.RunId);
        var jsonPath = Path.Combine(runsRoot, report.RunId, relative);

        Assert.True(File.Exists(jsonPath));
        var json = await File.ReadAllTextAsync(jsonPath);
        Assert.Contains(report.RunId, json, StringComparison.OrdinalIgnoreCase);
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

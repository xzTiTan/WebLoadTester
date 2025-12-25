using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain;
using WebLoadTester.Reports;
using WebLoadTester.Services.Strategies;

namespace WebLoadTester.Services;

public class TestOrchestrator
{
    private readonly ReportWriter _reportWriter = new();

    public async Task<TestRunResult> ExecuteAsync(TestType type, RunContext context, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var strategy = CreateStrategy(type);
        var results = await strategy.ExecuteAsync(context, ct);
        var finished = DateTime.UtcNow;

        var reportPath = await _reportWriter.WriteAsync(type, context.Settings, results, started, finished, ct);

        return new TestRunResult
        {
            Runs = results,
            ReportPath = reportPath,
            StartedAt = started,
            FinishedAt = finished
        };
    }

    private IRunStrategy CreateStrategy(TestType type) => type switch
    {
        TestType.E2E => new E2ERunStrategy(),
        TestType.Load => new LoadRunStrategy(),
        TestType.Stress => new StressRunStrategy(),
        TestType.Endurance => new EnduranceRunStrategy(),
        TestType.Screenshot => new ScreenshotRunStrategy(),
        _ => new E2ERunStrategy()
    };
}

public class TestRunResult
{
    public List<RunResult> Runs { get; set; } = new();
    public string ReportPath { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
}

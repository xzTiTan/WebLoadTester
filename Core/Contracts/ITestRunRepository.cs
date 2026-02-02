using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Репозиторий прогонов и их агрегатов.
/// </summary>
public interface ITestRunRepository
{
    Task CreateAsync(TestRun run, CancellationToken ct);
    Task UpdateStatusAsync(string runId, string status, DateTimeOffset? finishedAt, CancellationToken ct);
    Task UpdateSummaryAsync(string runId, string summaryJson, CancellationToken ct);
    Task<IReadOnlyList<TestRunSummary>> ListAsync(RunQuery query, CancellationToken ct);
    Task<TestRunDetail?> GetByIdAsync(string runId, CancellationToken ct);
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Контракт доступа к хранилищу прогонов, тестов и профилей.
/// </summary>
public interface IRunStore
{
    Task InitializeAsync(CancellationToken ct);

    Task<IReadOnlyList<TestCase>> GetTestCasesAsync(string moduleType, CancellationToken ct);
    Task<TestCaseVersion?> GetTestCaseVersionAsync(Guid testCaseId, int version, CancellationToken ct);
    Task<TestCase> SaveTestCaseAsync(string name, string description, string moduleType, string payloadJson, string changeNote, CancellationToken ct);
    Task DeleteTestCaseAsync(Guid testCaseId, CancellationToken ct);

    Task<IReadOnlyList<RunProfile>> GetRunProfilesAsync(CancellationToken ct);
    Task<RunProfile> SaveRunProfileAsync(RunProfile profile, CancellationToken ct);
    Task DeleteRunProfileAsync(Guid profileId, CancellationToken ct);

    Task CreateRunAsync(TestRun run, CancellationToken ct);
    Task UpdateRunAsync(TestRun run, CancellationToken ct);
    Task AddRunItemsAsync(IEnumerable<RunItem> items, CancellationToken ct);
    Task AddArtifactsAsync(IEnumerable<ArtifactRecord> artifacts, CancellationToken ct);
    Task AddTelegramNotificationAsync(TelegramNotification notification, CancellationToken ct);

    Task<IReadOnlyList<TestRunSummary>> QueryRunsAsync(RunQuery query, CancellationToken ct);
    Task<TestRunDetail?> GetRunDetailAsync(string runId, CancellationToken ct);
    Task DeleteRunAsync(string runId, CancellationToken ct);
}

/// <summary>
/// Параметры фильтрации списка прогонов.
/// </summary>
public class RunQuery
{
    public string? ModuleType { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? Search { get; set; }
}

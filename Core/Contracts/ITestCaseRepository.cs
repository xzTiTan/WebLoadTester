using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Репозиторий тестов и их версий.
/// </summary>
public interface ITestCaseRepository
{
    Task<IReadOnlyList<TestCase>> ListAsync(string moduleType, CancellationToken ct);
    Task<TestCase?> GetAsync(Guid testCaseId, CancellationToken ct);
    Task<TestCaseVersion?> GetVersionAsync(Guid testCaseId, int version, CancellationToken ct);
    Task<TestCase> SaveVersionAsync(string name, string description, string moduleType, string payloadJson, string changeNote, CancellationToken ct);
    Task SetCurrentVersionAsync(Guid testCaseId, int version, CancellationToken ct);
    Task DeleteAsync(Guid testCaseId, CancellationToken ct);
}

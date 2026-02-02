using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Репозиторий профилей запуска.
/// </summary>
public interface IRunProfileRepository
{
    Task<IReadOnlyList<RunProfile>> ListAsync(CancellationToken ct);
    Task<RunProfile?> GetAsync(Guid profileId, CancellationToken ct);
    Task<RunProfile> SaveAsync(RunProfile profile, CancellationToken ct);
    Task DeleteAsync(Guid profileId, CancellationToken ct);
}

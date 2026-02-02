using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Репозиторий артефактов прогона.
/// </summary>
public interface IArtifactRepository
{
    Task AddAsync(IEnumerable<ArtifactRecord> artifacts, CancellationToken ct);
    Task<IReadOnlyList<ArtifactRecord>> ListByRunAsync(string runId, CancellationToken ct);
}

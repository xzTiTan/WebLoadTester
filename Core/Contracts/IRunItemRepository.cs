using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Репозиторий элементов прогона.
/// </summary>
public interface IRunItemRepository
{
    Task AppendAsync(IEnumerable<RunItem> items, CancellationToken ct);
    Task<IReadOnlyList<RunItem>> ListByRunAsync(string runId, CancellationToken ct);
}

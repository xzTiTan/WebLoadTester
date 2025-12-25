using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain;

namespace WebLoadTester.Services.Strategies;

public interface IRunStrategy
{
    Task<List<RunResult>> ExecuteAsync(RunContext context, CancellationToken ct);
}

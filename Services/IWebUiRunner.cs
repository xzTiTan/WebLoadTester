using System;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain;

namespace WebLoadTester.Services
{
    public interface IWebUiRunner : IAsyncDisposable
    {
        Task<RunResult> RunOnceAsync(RunRequest request, CancellationToken ct);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain;

namespace WebLoadTester.Services;

public interface IWebUiRunner : IAsyncDisposable
{
    Task<RunResult> RunOnceAsync(Scenario scenario, RunSettings settings, int workerId, int runId, ILogSink log, CancellationToken ct);
}

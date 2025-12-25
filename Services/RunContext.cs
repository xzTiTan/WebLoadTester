using System;
using System.Threading;
using WebLoadTester.Domain;

namespace WebLoadTester.Services
{
    public class RunContext
    {
        public RunSettings Settings { get; init; } = new();
        public Scenario Scenario { get; init; } = new();
        public ILogSink Logger { get; init; } = default!;
        public IWebUiRunner Runner { get; init; } = default!;
        public CancellationTokenSource? Cancellation { get; init; }
        public Action<int, int>? Progress { get; init; }
    }
}

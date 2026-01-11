using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public sealed class ProgressTracker : IProgressSink
{
    public event Action<int, int, string?>? ProgressChanged;

    public void Report(int completed, int total, string? message = null)
    {
        ProgressChanged?.Invoke(completed, total, message);
    }
}

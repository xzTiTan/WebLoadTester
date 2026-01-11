using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public sealed class ProgressBus : IProgressSink
{
    public event Action<ProgressUpdate>? Progressed;

    public void Report(ProgressUpdate update)
    {
        Progressed?.Invoke(update);
    }
}

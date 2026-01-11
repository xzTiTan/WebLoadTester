using System;
using WebLoadTester.Core.Contracts;

namespace WebLoadTester.Core.Services;

public class ProgressBus : IProgressSink
{
    public event Action<ProgressUpdate>? ProgressChanged;

    public void Report(ProgressUpdate update)
    {
        ProgressChanged?.Invoke(update);
    }
}

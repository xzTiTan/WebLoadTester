namespace WebLoadTester.Core.Contracts;

public interface IProgressSink
{
    void Report(int completed, int total, string? message = null);
}

namespace WebLoadTester.Core.Contracts;

public interface IProgressSink
{
    void Report(ProgressUpdate update);
}

public sealed record ProgressUpdate(int Current, int Total, string Message);

namespace WebLoadTester.Core.Contracts;

public record ProgressUpdate(int Current, int Total, string Message);

public interface IProgressSink
{
    void Report(ProgressUpdate update);
}

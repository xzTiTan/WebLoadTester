using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

public interface IProgressSink
{
    void Report(ProgressUpdate update);
}

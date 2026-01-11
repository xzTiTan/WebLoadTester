using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

public class JsonReportWriter
{
    private readonly IArtifactStore _artifactStore;

    public JsonReportWriter(IArtifactStore artifactStore)
    {
        _artifactStore = artifactStore;
    }

    public Task<string> WriteAsync(TestReport report, string runFolder)
    {
        return _artifactStore.SaveJsonAsync(report, runFolder);
    }
}

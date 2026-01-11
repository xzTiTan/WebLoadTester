using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

public interface IArtifactStore
{
    string ReportsRoot { get; }
    string ScreenshotsRoot { get; }
    string ProfilesRoot { get; }
    string CreateRunFolder(string runId);
    Task<string> SaveJsonAsync(TestReport report, string runFolder);
    Task<string> SaveHtmlAsync(TestReport report, string runFolder, string html);
    Task<string> SaveScreenshotAsync(byte[] bytes, string runFolder, string fileName);
}

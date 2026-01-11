using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

public interface IArtifactStore
{
    string ReportsRoot { get; }
    string ScreenshotsRoot { get; }
    string ProfilesRoot { get; }
    string CreateRunFolder(string nameHint);
    Task<string> SaveJsonAsync(TestReport report);
    Task<string> SaveHtmlAsync(TestReport report, string htmlContent);
    Task<string> SaveScreenshotAsync(byte[] bytes, string runFolder, string fileName);
}

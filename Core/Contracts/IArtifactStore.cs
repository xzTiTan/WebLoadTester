using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

public interface IArtifactStore
{
    string BasePath { get; }
    string ReportsPath { get; }
    string ScreenshotsPath { get; }
    string ProfilesPath { get; }
    string CreateRunFolder(DateTimeOffset timestamp);
    Task<string> SaveJsonAsync(TestReport report, CancellationToken ct);
    Task<string> SaveHtmlAsync(TestReport report, string html, CancellationToken ct);
    Task<string> SaveScreenshotAsync(string runFolder, byte[] data, string fileName, CancellationToken ct);
}

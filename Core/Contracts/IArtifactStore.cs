using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

public interface IArtifactStore
{
    string ReportsFolder { get; }
    string ScreenshotsFolder { get; }
    string ProfilesFolder { get; }

    string CreateRunFolder(DateTimeOffset timestamp);
    string SaveJson(TestReport report, string runFolder);
    string SaveHtml(TestReport report, string runFolder);
    string SaveScreenshot(byte[] bytes, string runFolder, string name);
}

using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Storage;

public sealed class ArtifactStore : IArtifactStore
{
    public string ReportsFolder { get; }
    public string ScreenshotsFolder { get; }
    public string ProfilesFolder { get; }

    public ArtifactStore()
    {
        var baseFolder = AppContext.BaseDirectory;
        ReportsFolder = Path.Combine(baseFolder, "reports");
        ScreenshotsFolder = Path.Combine(baseFolder, "screenshots");
        ProfilesFolder = Path.Combine(baseFolder, "profiles");

        Directory.CreateDirectory(ReportsFolder);
        Directory.CreateDirectory(ScreenshotsFolder);
        Directory.CreateDirectory(ProfilesFolder);
    }

    public string CreateRunFolder(DateTimeOffset timestamp)
    {
        var folder = Path.Combine(ScreenshotsFolder, timestamp.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    public string SaveJson(TestReport report, string runFolder)
    {
        return report.Artifacts.JsonPath ?? string.Empty;
    }

    public string SaveHtml(TestReport report, string runFolder)
    {
        return report.Artifacts.HtmlPath ?? string.Empty;
    }

    public string SaveScreenshot(byte[] bytes, string runFolder, string name)
    {
        var safeName = $"{name}.png";
        var path = Path.Combine(runFolder, safeName);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}

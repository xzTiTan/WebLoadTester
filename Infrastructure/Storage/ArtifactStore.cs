using System.Text;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services.ReportWriters;

namespace WebLoadTester.Infrastructure.Storage;

public sealed class ArtifactStore : IArtifactStore
{
    private readonly JsonReportWriter _jsonWriter;

    public ArtifactStore(JsonReportWriter jsonWriter)
    {
        _jsonWriter = jsonWriter;
        BasePath = AppContext.BaseDirectory;
        ReportsPath = Path.Combine(BasePath, "reports");
        ScreenshotsPath = Path.Combine(BasePath, "screenshots");
        ProfilesPath = Path.Combine(BasePath, "profiles");
        Directory.CreateDirectory(ReportsPath);
        Directory.CreateDirectory(ScreenshotsPath);
        Directory.CreateDirectory(ProfilesPath);
    }

    public string BasePath { get; }
    public string ReportsPath { get; }
    public string ScreenshotsPath { get; }
    public string ProfilesPath { get; }

    public string CreateRunFolder(DateTimeOffset timestamp)
    {
        var folder = Path.Combine(ScreenshotsPath, timestamp.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    public async Task<string> SaveJsonAsync(TestReport report, CancellationToken ct)
    {
        var name = $"report_{report.StartedAt:yyyyMMdd_HHmmss}.json";
        var path = Path.Combine(ReportsPath, name);
        var payload = _jsonWriter.Serialize(report);
        await File.WriteAllTextAsync(path, payload, Encoding.UTF8, ct);
        return path;
    }

    public async Task<string> SaveHtmlAsync(TestReport report, string html, CancellationToken ct)
    {
        var name = $"report_{report.StartedAt:yyyyMMdd_HHmmss}.html";
        var path = Path.Combine(ReportsPath, name);
        await File.WriteAllTextAsync(path, html, Encoding.UTF8, ct);
        return path;
    }

    public async Task<string> SaveScreenshotAsync(string runFolder, byte[] data, string fileName, CancellationToken ct)
    {
        var path = Path.Combine(runFolder, fileName);
        await File.WriteAllBytesAsync(path, data, ct);
        return path;
    }
}

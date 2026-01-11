using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Storage;

public class ArtifactStore : IArtifactStore
{
    public ArtifactStore()
    {
        ReportsRoot = Path.Combine(AppContext.BaseDirectory, "reports");
        ScreenshotsRoot = Path.Combine(AppContext.BaseDirectory, "screenshots");
        ProfilesRoot = Path.Combine(AppContext.BaseDirectory, "profiles");
        Directory.CreateDirectory(ReportsRoot);
        Directory.CreateDirectory(ScreenshotsRoot);
        Directory.CreateDirectory(ProfilesRoot);
    }

    public string ReportsRoot { get; }
    public string ScreenshotsRoot { get; }
    public string ProfilesRoot { get; }

    public string CreateRunFolder(string runId)
    {
        var folder = Path.Combine(ScreenshotsRoot, runId);
        Directory.CreateDirectory(folder);
        return folder;
    }

    public async Task<string> SaveJsonAsync(TestReport report, string runFolder)
    {
        Directory.CreateDirectory(ReportsRoot);
        var fileName = $"report_{report.StartedAt:yyyyMMdd_HHmmss}.json";
        var path = Path.Combine(ReportsRoot, fileName);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    public async Task<string> SaveHtmlAsync(TestReport report, string runFolder, string html)
    {
        Directory.CreateDirectory(ReportsRoot);
        var fileName = $"report_{report.StartedAt:yyyyMMdd_HHmmss}.html";
        var path = Path.Combine(ReportsRoot, fileName);
        await File.WriteAllTextAsync(path, html);
        return path;
    }

    public async Task<string> SaveScreenshotAsync(byte[] bytes, string runFolder, string fileName)
    {
        var path = Path.Combine(runFolder, fileName);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }
}

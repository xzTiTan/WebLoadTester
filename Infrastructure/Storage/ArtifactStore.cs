using System;
using System.IO;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Storage;

public sealed class ArtifactStore : IArtifactStore
{
    public ArtifactStore()
    {
        var baseDir = AppContext.BaseDirectory;
        ReportsRoot = Path.Combine(baseDir, "reports");
        ScreenshotsRoot = Path.Combine(baseDir, "screenshots");
        ProfilesRoot = Path.Combine(baseDir, "profiles");
        Directory.CreateDirectory(ReportsRoot);
        Directory.CreateDirectory(ScreenshotsRoot);
        Directory.CreateDirectory(ProfilesRoot);
    }

    public string ReportsRoot { get; }
    public string ScreenshotsRoot { get; }
    public string ProfilesRoot { get; }

    public string CreateRunFolder(string nameHint)
    {
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
        var folder = Path.Combine(ScreenshotsRoot, $"{nameHint}_{stamp}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public Task<string> SaveJsonAsync(TestReport report)
    {
        var fileName = $"report_{report.ModuleId}_{report.StartedAt:yyyyMMdd_HHmmss}.json";
        var path = Path.Combine(ReportsRoot, fileName);
        return Task.FromResult(path);
    }

    public Task<string> SaveHtmlAsync(TestReport report, string htmlContent)
    {
        var fileName = $"report_{report.ModuleId}_{report.StartedAt:yyyyMMdd_HHmmss}.html";
        var path = Path.Combine(ReportsRoot, fileName);
        return Task.FromResult(path);
    }

    public async Task<string> SaveScreenshotAsync(byte[] bytes, string runFolder, string fileName)
    {
        Directory.CreateDirectory(runFolder);
        var path = Path.Combine(runFolder, fileName);
        await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
        return path;
    }
}

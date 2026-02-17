using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services;

public static class WorkerArtifactPathBuilder
{
    public static string GetWorkerProfilesDir(string runDir, int workerId)
    {
        return Path.Combine(runDir, "profiles", $"w{workerId}");
    }

    public static string GetWorkerScreenshotsDir(string runDir, int workerId, int iteration)
    {
        return Path.Combine(runDir, "screenshots", $"w{workerId}", $"it{iteration}");
    }

    public static string GetWorkerProfileRelativePath(int workerId, string fileName)
    {
        return Path.Combine("profiles", $"w{workerId}", fileName);
    }

    public static string GetWorkerScreenshotStoreRelativePath(int workerId, int iteration, string fileName)
    {
        return Path.Combine($"w{workerId}", $"it{iteration}", fileName);
    }

    public static string GetWorkerScreenshotArtifactRelativePath(int workerId, int iteration, string fileName)
    {
        return Path.Combine("screenshots", GetWorkerScreenshotStoreRelativePath(workerId, iteration, fileName));
    }

    public static async Task<IReadOnlyList<ModuleArtifact>> EnsureWorkerProfileSnapshotsAsync(IRunContext ctx, object moduleSettings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var artifacts = new List<ModuleArtifact>();
        var profilesDir = GetWorkerProfilesDir(ctx.RunFolder, ctx.WorkerId);
        Directory.CreateDirectory(profilesDir);

        var profilePath = Path.Combine(profilesDir, "profile.json");
        if (!File.Exists(profilePath))
        {
            var json = JsonSerializer.Serialize(ctx.Profile);
            await File.WriteAllTextAsync(profilePath, json, ct);
            artifacts.Add(new ModuleArtifact
            {
                Type = "ProfileSnapshot",
                RelativePath = GetWorkerProfileRelativePath(ctx.WorkerId, "profile.json")
            });
        }

        var moduleSettingsPath = Path.Combine(profilesDir, "moduleSettings.json");
        if (!File.Exists(moduleSettingsPath))
        {
            var json = JsonSerializer.Serialize(moduleSettings, moduleSettings.GetType());
            await File.WriteAllTextAsync(moduleSettingsPath, json, ct);
            artifacts.Add(new ModuleArtifact
            {
                Type = "ModuleSettingsSnapshot",
                RelativePath = GetWorkerProfileRelativePath(ctx.WorkerId, "moduleSettings.json")
            });
        }

        return artifacts;
    }
}

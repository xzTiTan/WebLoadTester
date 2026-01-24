using System;
using System.IO;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Storage;

/// <summary>
/// Реализация хранилища артефактов на файловой системе.
/// </summary>
public class ArtifactStore : IArtifactStore
{
    /// <summary>
    /// Инициализирует папки для отчётов, скриншотов и профилей.
    /// </summary>
    public ArtifactStore(string runsRoot, string profilesRoot)
    {
        RunsRoot = runsRoot;
        ProfilesRoot = profilesRoot;
        Directory.CreateDirectory(RunsRoot);
        Directory.CreateDirectory(ProfilesRoot);
    }

    public string RunsRoot { get; }
    public string ProfilesRoot { get; }

    /// <summary>
    /// Создаёт папку для конкретного запуска и возвращает путь.
    /// </summary>
    public string CreateRunFolder(string runId)
    {
        var folder = Path.Combine(RunsRoot, runId);
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "screenshots"));
        Directory.CreateDirectory(Path.Combine(folder, "logs"));
        return folder;
    }

    /// <summary>
    /// Возвращает путь к файлу лога прогона.
    /// </summary>
    public string GetLogPath(string runId)
    {
        return Path.Combine(RunsRoot, runId, "logs", "run.log");
    }

    /// <summary>
    /// Сохраняет отчёт в JSON-файл.
    /// </summary>
    public async Task<string> SaveJsonAsync(string json, string runFolder)
    {
        Directory.CreateDirectory(runFolder);
        var path = Path.Combine(runFolder, "report.json");
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    /// <summary>
    /// Сохраняет отчёт в HTML-файл.
    /// </summary>
    public async Task<string> SaveHtmlAsync(TestReport report, string runFolder, string html)
    {
        Directory.CreateDirectory(runFolder);
        var path = Path.Combine(runFolder, "report.html");
        await File.WriteAllTextAsync(path, html);
        return path;
    }

    /// <summary>
    /// Сохраняет скриншот в указанную папку.
    /// </summary>
    public async Task<string> SaveScreenshotAsync(byte[] bytes, string runFolder, string fileName)
    {
        var path = Path.Combine(runFolder, "screenshots", fileName);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }
}

using System.IO;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;

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
    /// Добавляет строку в лог прогона и возвращает относительный путь.
    /// </summary>
    public async Task<string> AppendLogLineAsync(string runId, string line)
    {
        var folder = CreateRunFolder(runId);
        var relative = Path.Combine("logs", "run.log");
        var path = Path.Combine(folder, relative);
        await File.AppendAllTextAsync(path, line + System.Environment.NewLine);
        return relative;
    }

    /// <summary>
    /// Сохраняет отчёт в JSON-файл и возвращает относительный путь.
    /// </summary>
    public async Task<string> SaveJsonReportAsync(string runId, string json)
    {
        var folder = CreateRunFolder(runId);
        var relative = "report.json";
        var path = Path.Combine(folder, relative);
        await File.WriteAllTextAsync(path, json);
        return relative;
    }

    /// <summary>
    /// Сохраняет отчёт в HTML-файл и возвращает относительный путь.
    /// </summary>
    public async Task<string> SaveHtmlReportAsync(string runId, string html)
    {
        var folder = CreateRunFolder(runId);
        var relative = "report.html";
        var path = Path.Combine(folder, relative);
        await File.WriteAllTextAsync(path, html);
        return relative;
    }

    /// <summary>
    /// Сохраняет скриншот в папку и возвращает относительный путь.
    /// </summary>
    public async Task<string> SaveScreenshotAsync(string runId, string fileName, byte[] bytes)
    {
        var folder = CreateRunFolder(runId);
        var relative = Path.Combine("screenshots", fileName);
        var path = Path.Combine(folder, relative);
        await File.WriteAllBytesAsync(path, bytes);
        return relative;
    }
}

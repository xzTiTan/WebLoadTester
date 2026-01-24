using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Контракт для хранения артефактов запуска: отчётов и скриншотов.
/// </summary>
public interface IArtifactStore
{
    /// <summary>
    /// Корневая папка прогонов.
    /// </summary>
    string RunsRoot { get; }
    /// <summary>
    /// Корневая папка профилей.
    /// </summary>
    string ProfilesRoot { get; }
    /// <summary>
    /// Создаёт папку для конкретного запуска.
    /// </summary>
    string CreateRunFolder(string runId);
    /// <summary>
    /// Возвращает путь к файлу лога для прогона.
    /// </summary>
    string GetLogPath(string runId);
    /// <summary>
    /// Сохраняет отчёт в формате JSON.
    /// </summary>
    Task<string> SaveJsonAsync(string json, string runFolder);
    /// <summary>
    /// Сохраняет отчёт в формате HTML.
    /// </summary>
    Task<string> SaveHtmlAsync(TestReport report, string runFolder, string html);
    /// <summary>
    /// Сохраняет скриншот в файл и возвращает путь.
    /// </summary>
    Task<string> SaveScreenshotAsync(byte[] bytes, string runFolder, string fileName);
}

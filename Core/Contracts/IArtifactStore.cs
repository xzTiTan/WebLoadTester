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
    /// Создаёт папку для конкретного запуска.
    /// </summary>
    string CreateRunFolder(string runId);
    /// <summary>
    /// Возвращает путь к файлу лога для прогона.
    /// </summary>
    string GetLogPath(string runId);
    /// <summary>
    /// Добавляет строку в файл лога прогона.
    /// </summary>
    Task<string> AppendLogLineAsync(string runId, string line);
    /// <summary>
    /// Сохраняет отчёт в формате JSON и возвращает относительный путь.
    /// </summary>
    Task<string> SaveJsonReportAsync(string runId, string json);
    /// <summary>
    /// Сохраняет отчёт в формате HTML и возвращает относительный путь.
    /// </summary>
    Task<string> SaveHtmlReportAsync(string runId, string html);
    /// <summary>
    /// Сохраняет скриншот в файл и возвращает относительный путь.
    /// </summary>
    Task<string> SaveScreenshotAsync(string runId, string fileName, byte[] bytes);
}

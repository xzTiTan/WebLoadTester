using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Контракт для хранения артефактов запуска: отчётов и скриншотов.
/// </summary>
public interface IArtifactStore
{
    /// <summary>
    /// Корневая папка отчётов.
    /// </summary>
    string ReportsRoot { get; }
    /// <summary>
    /// Корневая папка скриншотов.
    /// </summary>
    string ScreenshotsRoot { get; }
    /// <summary>
    /// Корневая папка профилей.
    /// </summary>
    string ProfilesRoot { get; }
    /// <summary>
    /// Создаёт папку для конкретного запуска.
    /// </summary>
    string CreateRunFolder(string runId);
    /// <summary>
    /// Сохраняет отчёт в формате JSON.
    /// </summary>
    Task<string> SaveJsonAsync(TestReport report, string runFolder);
    /// <summary>
    /// Сохраняет отчёт в формате HTML.
    /// </summary>
    Task<string> SaveHtmlAsync(TestReport report, string runFolder, string html);
    /// <summary>
    /// Сохраняет скриншот в файл и возвращает путь.
    /// </summary>
    Task<string> SaveScreenshotAsync(byte[] bytes, string runFolder, string fileName);
}

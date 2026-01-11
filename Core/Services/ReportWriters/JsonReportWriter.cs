using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Services.ReportWriters;

/// <summary>
/// Писатель JSON-отчётов через хранилище артефактов.
/// </summary>
public class JsonReportWriter
{
    private readonly IArtifactStore _artifactStore;

    /// <summary>
    /// Создаёт писатель с заданным хранилищем.
    /// </summary>
    public JsonReportWriter(IArtifactStore artifactStore)
    {
        _artifactStore = artifactStore;
    }

    /// <summary>
    /// Сохраняет отчёт в JSON и возвращает путь к файлу.
    /// </summary>
    public Task<string> WriteAsync(TestReport report, string runFolder)
    {
        return _artifactStore.SaveJsonAsync(report, runFolder);
    }
}

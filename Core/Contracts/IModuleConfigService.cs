using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Core.Contracts;

/// <summary>
/// Сервис управления конфигурациями модулей.
/// </summary>
public interface IModuleConfigService
{
    Task<IReadOnlyList<ModuleConfigSummary>> ListAsync(string moduleKey, CancellationToken ct);
    Task<ModuleConfigPayload?> LoadAsync(string finalName, CancellationToken ct);
    Task<string> SaveNewAsync(string userName, string moduleKey, string description, object moduleSettings, RunParametersDto runParameters, CancellationToken ct);
    Task SaveOverwriteAsync(string finalName, string moduleKey, string description, object moduleSettings, RunParametersDto runParameters, CancellationToken ct);
    Task DeleteAsync(string finalName, CancellationToken ct);
}

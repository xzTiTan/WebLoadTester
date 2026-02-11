using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Infrastructure.Storage;

/// <summary>
/// SQLite-реализация управления конфигурациями модулей через TestCase/TestCaseVersion.
/// </summary>
public sealed class ModuleConfigService : IModuleConfigService
{
    private readonly ITestCaseRepository _testCaseRepository;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ModuleConfigService(ITestCaseRepository testCaseRepository)
    {
        _testCaseRepository = testCaseRepository;
    }

    public async Task<IReadOnlyList<ModuleConfigSummary>> ListAsync(string moduleKey, CancellationToken ct)
    {
        var cases = await _testCaseRepository.ListAsync(moduleKey, ct);
        return cases.Select(testCase => new ModuleConfigSummary
        {
            Id = testCase.Id,
            FinalName = testCase.Name,
            Description = testCase.Description,
            ModuleKey = testCase.ModuleType,
            CurrentVersion = testCase.CurrentVersion,
            UpdatedAt = testCase.UpdatedAt
        }).ToList();
    }

    public async Task<ModuleConfigPayload?> LoadAsync(string finalName, CancellationToken ct)
    {
        var match = await _testCaseRepository.GetByNameAsync(finalName, ct);
        if (match == null)
        {
            return null;
        }

        var version = await _testCaseRepository.GetVersionAsync(match.Id, match.CurrentVersion, ct);
        if (version == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<ModuleConfigPayload>(version.PayloadJson, _jsonOptions);
    }

    public async Task<string> SaveNewAsync(string userName, string moduleKey, string description, object moduleSettings, RunParametersDto runParameters, CancellationToken ct)
    {
        var normalizedUserName = NormalizeUserName(userName);
        var moduleSuffix = ModuleCatalog.GetSuffix(moduleKey);
        var finalName = $"{normalizedUserName}_{moduleSuffix}";

        var payload = BuildPayload(userName, finalName, moduleKey, description, moduleSettings, runParameters);
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        await _testCaseRepository.SaveVersionAsync(finalName, description, moduleKey, json, "Создание конфигурации", ct);
        return finalName;
    }

    public async Task SaveOverwriteAsync(string finalName, string moduleKey, string description, object moduleSettings, RunParametersDto runParameters, CancellationToken ct)
    {
        var payload = BuildPayload(ParseUserName(finalName), finalName, moduleKey, description, moduleSettings, runParameters);
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        await _testCaseRepository.SaveVersionAsync(finalName, description, moduleKey, json, "Обновление конфигурации", ct);
    }

    public async Task DeleteAsync(string finalName, CancellationToken ct)
    {
        var match = await _testCaseRepository.GetByNameAsync(finalName, ct);
        if (match == null)
        {
            return;
        }

        await _testCaseRepository.DeleteAsync(match.Id, ct);
    }

    private ModuleConfigPayload BuildPayload(string userName, string finalName, string moduleKey, string description, object moduleSettings, RunParametersDto runParameters)
    {
        return new ModuleConfigPayload
        {
            ModuleKey = moduleKey,
            ModuleSettings = JsonSerializer.SerializeToElement(moduleSettings, _jsonOptions),
            RunParameters = runParameters,
            Meta = new ModuleConfigMeta
            {
                UserName = userName,
                FinalName = finalName,
                Description = description,
                CreatedAt = DateTimeOffset.Now
            }
        };
    }

    private static string NormalizeUserName(string userName)
    {
        return string.Join(string.Empty, userName.Where(ch => !char.IsWhiteSpace(ch))).Trim();
    }

    private static string ParseUserName(string finalName)
    {
        var index = finalName.IndexOf('_', StringComparison.Ordinal);
        return index > 0 ? finalName[..index] : finalName;
    }
}

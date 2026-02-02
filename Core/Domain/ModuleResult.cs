using System.Collections.Generic;

namespace WebLoadTester.Core.Domain;

/// <summary>
/// Результат выполнения одного запуска модуля.
/// </summary>
public class ModuleResult
{
    public TestStatus Status { get; set; } = TestStatus.Success;
    public List<ResultBase> Results { get; set; } = new();
    public List<ModuleArtifact> Artifacts { get; set; } = new();
    public string? SummaryMessage { get; set; }
}

/// <summary>
/// Артефакт, созданный модулем.
/// </summary>
public class ModuleArtifact
{
    public string Type { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}

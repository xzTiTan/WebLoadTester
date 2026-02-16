using System;
using System.Text.Json;

namespace WebLoadTester.Core.Domain;

/// <summary>
/// Полезная нагрузка конфигурации модуля.
/// </summary>
public sealed class ModuleConfigPayload
{
    public string ModuleKey { get; set; } = string.Empty;
    public JsonElement ModuleSettings { get; set; }
    public RunParametersDto RunParameters { get; set; } = new();
    public ModuleConfigMeta Meta { get; set; } = new();
}

/// <summary>
/// Метаданные конфигурации модуля.
/// </summary>
public sealed class ModuleConfigMeta
{
    public string UserName { get; set; } = string.Empty;
    public string FinalName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// DTO параметров запуска для сохранения в конфигурации.
/// </summary>
public sealed class RunParametersDto
{
    public RunMode Mode { get; set; } = RunMode.Iterations;
    public int Iterations { get; set; } = 10;
    public int DurationSeconds { get; set; } = 60;
    public int Parallelism { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 30;
    public int PauseBetweenIterationsMs { get; set; }
    public bool HtmlReportEnabled { get; set; }
    public bool TelegramEnabled { get; set; }
    public bool PreflightEnabled { get; set; }
    public bool Headless { get; set; } = true;
    public ScreenshotsPolicy ScreenshotsPolicy { get; set; } = ScreenshotsPolicy.OnError;
}

/// <summary>
/// Сводка по конфигурации модуля.
/// </summary>
public sealed class ModuleConfigSummary
{
    public Guid Id { get; set; }
    public string FinalName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ModuleKey { get; set; } = string.Empty;
    public int CurrentVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

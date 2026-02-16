using System;
using System.Collections.Generic;

namespace WebLoadTester.Core.Domain;

/// <summary>
/// Тестовый сценарий с текущей версией.
/// </summary>
public class TestCase
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ModuleType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int CurrentVersion { get; set; }
}

/// <summary>
/// Версия тестового сценария.
/// </summary>
public class TestCaseVersion
{
    public Guid Id { get; set; }
    public Guid TestCaseId { get; set; }
    public int VersionNumber { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string ChangeNote { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
}

/// <summary>
/// Профиль запуска (шаблон параметров).
/// </summary>
public class RunProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Parallelism { get; set; } = 2;
    public RunMode Mode { get; set; } = RunMode.Iterations;
    public int Iterations { get; set; } = 10;
    public int DurationSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 30;
    public int PauseBetweenIterationsMs { get; set; }
    public bool Headless { get; set; } = true;
    public ScreenshotsPolicy ScreenshotsPolicy { get; set; } = ScreenshotsPolicy.OnError;
    public bool HtmlReportEnabled { get; set; }
    public bool TelegramEnabled { get; set; }
    public bool PreflightEnabled { get; set; }
}

/// <summary>
/// Режим запуска по итерациям или длительности.
/// </summary>
public enum RunMode
{
    Iterations,
    Duration
}

/// <summary>
/// Политика скриншотов.
/// </summary>
public enum ScreenshotsPolicy
{
    Off,
    OnError,
    Always
}

/// <summary>
/// Сущность прогона для хранения в БД.
/// </summary>
public class TestRun
{
    public string RunId { get; set; } = string.Empty;
    public Guid TestCaseId { get; set; }
    public int TestCaseVersion { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string ModuleType { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string ProfileSnapshotJson { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = "Running";
    public string SummaryJson { get; set; } = string.Empty;
}

/// <summary>
/// Детальная запись по элементу проверки.
/// </summary>
public class RunItem
{
    public Guid Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string ItemKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public int WorkerId { get; set; }
    public int Iteration { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ExtraJson { get; set; }
}

/// <summary>
/// Запись об артефакте прогона.
/// </summary>
public class ArtifactRecord
{
    public Guid Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string ArtifactType { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Результат отправки Telegram-уведомления.
/// </summary>
public class TelegramNotification
{
    public Guid Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Детали прогона для вкладки истории.
/// </summary>
public class TestRunDetail
{
    public TestRun Run { get; set; } = new();
    public IReadOnlyList<RunItem> Items { get; set; } = Array.Empty<RunItem>();
    public IReadOnlyList<ArtifactRecord> Artifacts { get; set; } = Array.Empty<ArtifactRecord>();
}

/// <summary>
/// Краткая запись прогона для списка.
/// </summary>
public class TestRunSummary
{
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public int FailedItems { get; set; }
    public string KeyMetrics { get; set; } = string.Empty;
    public string ModuleType { get; set; } = string.Empty;
}

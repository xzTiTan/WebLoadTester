using System.Collections.Generic;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Modules.UiSnapshot;

/// <summary>
/// Настройки массового снятия UI-снимков.
/// </summary>
public class UiSnapshotSettings
{
    public List<SnapshotTarget> Targets { get; set; } = new();
    public UiWaitUntil WaitUntil { get; set; } = UiWaitUntil.DomContentLoaded;
    public int TimeoutSeconds { get; set; } = 30;
    public string ScreenshotFormat { get; set; } = "png";
    public bool FullPage { get; set; } = true;
    public int? ViewportWidth { get; set; }
    public int? ViewportHeight { get; set; }
}

public class SnapshotTarget
{
    public string Url { get; set; } = string.Empty;
    public string? Selector { get; set; }
    public string? Name { get; set; }

    /// <summary>
    /// Legacy-поле старых конфигураций. Используется для миграции в Name.
    /// </summary>
    public string? Tag { get; set; }
}

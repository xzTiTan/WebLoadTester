using System.Collections.Generic;

namespace WebLoadTester.Modules.UiSnapshot;

/// <summary>
/// Настройки снятия скриншотов страниц.
/// </summary>
public class UiSnapshotSettings
{
    public List<SnapshotTarget> Targets { get; set; } = new();
    public string WaitUntil { get; set; } = "load";
    public int TimeoutSeconds { get; set; } = 30;
    public string ScreenshotFormat { get; set; } = "png";
    public int? ViewportWidth { get; set; }
    public int? ViewportHeight { get; set; }
    public bool FullPage { get; set; } = true;
}

public class SnapshotTarget
{
    public string Url { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string? Selector { get; set; }
}

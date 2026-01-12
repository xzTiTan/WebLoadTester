using System.Collections.Generic;

namespace WebLoadTester.Modules.UiSnapshot;

/// <summary>
/// Настройки снятия скриншотов страниц.
/// </summary>
public class UiSnapshotSettings
{
    public List<SnapshotTarget> Targets { get; set; } = new();
    public int Concurrency { get; set; } = 2;
    public int RepeatsPerUrl { get; set; } = 1;
    public string WaitUntil { get; set; } = "load";
    public int ExtraDelayMs { get; set; } = 0;
    public bool FullPage { get; set; } = true;
}

public class SnapshotTarget
{
    public string Url { get; set; } = string.Empty;
    public string? Tag { get; set; }
}

using System.Collections.Generic;

namespace WebLoadTester.Modules.UiTiming;

/// <summary>
/// Настройки замеров времени загрузки страниц.
/// </summary>
public class UiTimingSettings
{
    public List<TimingTarget> Targets { get; set; } = new();
    public string WaitUntil { get; set; } = "load";
    public int TimeoutSeconds { get; set; } = 30;
}

public class TimingTarget
{
    public string Url { get; set; } = string.Empty;
    public string? Tag { get; set; }
}

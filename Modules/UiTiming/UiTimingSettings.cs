using System.Collections.Generic;

namespace WebLoadTester.Modules.UiTiming;

/// <summary>
/// Настройки замеров времени загрузки страниц.
/// </summary>
public class UiTimingSettings
{
    public List<TimingTarget> Targets { get; set; } = new();
    public int RepeatsPerUrl { get; set; } = 3;
    public int Concurrency { get; set; } = 2;
    public string WaitUntil { get; set; } = "load";
    public bool Headless { get; set; } = true;
    public int TimeoutMs { get; set; } = 30000;
}

public class TimingTarget
{
    public string Url { get; set; } = string.Empty;
    public string? Tag { get; set; }
}

using System.Collections.Generic;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Modules.UiTiming;

/// <summary>
/// Настройки замеров времени загрузки страниц.
/// </summary>
public class UiTimingSettings
{
    public List<TimingTarget> Targets { get; set; } = new();
    public UiWaitUntil WaitUntil { get; set; } = UiWaitUntil.DomContentLoaded;
    public int TimeoutSeconds { get; set; } = 30;
}

public class TimingTarget
{
    public string Url { get; set; } = string.Empty;
}

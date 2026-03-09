using System.Collections.Generic;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Modules.UiTiming;

/// <summary>
/// Настройки тестирования совместимости Web-сайта.
/// </summary>
public class UiTimingSettings
{
    public List<TimingTarget> Targets { get; set; } = new();
    public UiWaitUntil WaitUntil { get; set; } = UiWaitUntil.DomContentLoaded;
    public int TimeoutSeconds { get; set; } = 30;
}

public class TimingTarget
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string BrowserChannel { get; set; } = "chromium";
    public int ViewportWidth { get; set; } = 1366;
    public int ViewportHeight { get; set; } = 768;
    public string UserAgent { get; set; } = string.Empty;
    public bool? Headless { get; set; }
}

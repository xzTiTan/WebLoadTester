using System.Collections.Generic;

namespace WebLoadTester.Modules.UiTiming;

/// <summary>
/// Настройки замеров времени загрузки страниц.
/// </summary>
public class UiTimingSettings
{
    public string BaseUrl { get; set; } = "https://example.com";
    public List<string> Paths { get; set; } = new();
    public int RepeatsPerUrl { get; set; } = 3;
    public int Concurrency { get; set; } = 2;
    public string WaitUntil { get; set; } = "load";
}

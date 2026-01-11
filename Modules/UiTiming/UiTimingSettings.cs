using System.Collections.Generic;

namespace WebLoadTester.Modules.UiTiming;

public sealed class UiTimingSettings
{
    public List<UrlEntry> Urls { get; set; } = new() { new UrlEntry { Url = "https://example.com" } };
    public int RepeatsPerUrl { get; set; } = 3;
    public int Concurrency { get; set; } = 2;
    public string WaitUntil { get; set; } = "load";
}

public sealed class UrlEntry
{
    public string Url { get; set; } = string.Empty;
}

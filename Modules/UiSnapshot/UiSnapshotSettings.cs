using System.Collections.Generic;

namespace WebLoadTester.Modules.UiSnapshot;

public sealed class UiSnapshotSettings
{
    public List<UrlEntry> Urls { get; set; } = new() { new UrlEntry { Url = "https://example.com" } };
    public int Concurrency { get; set; } = 2;
    public string WaitUntil { get; set; } = "load";
    public int DelayAfterLoadMs { get; set; } = 0;
}

public sealed class UrlEntry
{
    public string Url { get; set; } = string.Empty;
}

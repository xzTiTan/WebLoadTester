namespace WebLoadTester.Modules.UiTiming;

public sealed class UiTimingSettings
{
    public List<string> Urls { get; set; } = new() { "https://example.com" };
    public int RepeatsPerUrl { get; set; } = 3;
    public int Concurrency { get; set; } = 2;
    public string WaitUntil { get; set; } = "domcontentloaded";
    public bool Headless { get; set; } = true;
}

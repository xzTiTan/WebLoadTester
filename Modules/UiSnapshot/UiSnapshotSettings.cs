namespace WebLoadTester.Modules.UiSnapshot;

public sealed class UiSnapshotSettings
{
    public List<string> Urls { get; set; } = new() { "https://example.com" };
    public int Concurrency { get; set; } = 2;
    public string WaitMode { get; set; } = "domcontentloaded";
    public int DelayAfterLoadMs { get; set; } = 0;
    public bool Headless { get; set; } = true;
}

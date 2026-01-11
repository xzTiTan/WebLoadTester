namespace WebLoadTester.Modules.UiSnapshot;

public sealed class UiSnapshotSettings
{
    public List<string> Urls { get; set; } = new() { "https://example.com" };
    public int Concurrency { get; set; } = 2;
    public UiWaitMode WaitMode { get; set; } = UiWaitMode.DomContentLoaded;
    public int DelayAfterLoadMs { get; set; } = 0;

    public string UrlsText
    {
        get => string.Join(Environment.NewLine, Urls);
        set => Urls = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}

public enum UiWaitMode
{
    DomContentLoaded,
    Load
}

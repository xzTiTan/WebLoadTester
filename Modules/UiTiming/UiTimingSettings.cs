namespace WebLoadTester.Modules.UiTiming;

public sealed class UiTimingSettings
{
    public List<string> Urls { get; set; } = new() { "https://example.com" };
    public int RepeatsPerUrl { get; set; } = 3;
    public int Concurrency { get; set; } = 2;
    public UiTimingWaitUntil WaitUntil { get; set; } = UiTimingWaitUntil.DomContentLoaded;

    public string UrlsText
    {
        get => string.Join(Environment.NewLine, Urls);
        set => Urls = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}

public enum UiTimingWaitUntil
{
    DomContentLoaded,
    Load
}

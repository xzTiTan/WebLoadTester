namespace WebLoadTester.Modules.HttpAssets;

public sealed class HttpAssetsSettings
{
    public List<HttpAsset> Assets { get; set; } = new()
    {
        new HttpAsset { Url = "https://example.com" }
    };
    public string? ExpectedContentType { get; set; }
    public int? MaxSizeBytes { get; set; }
    public int? MaxLatencyMs { get; set; }

    public string AssetsText
    {
        get => string.Join(Environment.NewLine, Assets.Select(a => a.Url));
        set => Assets = value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(url => new HttpAsset { Url = url }).ToList();
    }
}

public sealed class HttpAsset
{
    public string Url { get; set; } = string.Empty;
}

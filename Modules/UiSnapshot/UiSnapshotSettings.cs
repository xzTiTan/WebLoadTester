using System.Collections.Generic;

namespace WebLoadTester.Modules.UiSnapshot;

/// <summary>
/// Настройки снятия скриншотов страниц.
/// </summary>
public class UiSnapshotSettings
{
    public string BaseUrl { get; set; } = "https://example.com";
    public List<string> Paths { get; set; } = new();
    public int Concurrency { get; set; } = 2;
    public string WaitMode { get; set; } = "load";
    public int DelayAfterLoadMs { get; set; } = 0;
}

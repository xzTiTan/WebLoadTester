using System.Collections.Generic;

namespace WebLoadTester.Modules.UiSnapshot;

public class UiSnapshotSettings
{
    public List<string> Urls { get; set; } = new();
    public int Concurrency { get; set; } = 2;
    public string WaitMode { get; set; } = "load";
    public int DelayAfterLoadMs { get; set; } = 0;
}

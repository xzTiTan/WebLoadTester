namespace WebLoadTester.Infrastructure.Storage;

public class UiLayoutState
{
    public double LeftNavWidth { get; set; } = 250;
    public double DetailsWidth { get; set; } = 290;
    public bool IsDetailsVisible { get; set; } = true;
    public bool IsLogExpanded { get; set; }
    public bool IsLogOnlyErrors { get; set; }
    public string LogFilterText { get; set; } = string.Empty;
}

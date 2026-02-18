namespace WebLoadTester.Infrastructure.Storage;

public class UiLayoutState
{
    public double LeftNavWidth { get; set; } = 280;
    public double DetailsWidth { get; set; } = 340;
    public bool IsDetailsVisible { get; set; } = true;
    public bool IsLogExpanded { get; set; }
    public bool IsLogOnlyErrors { get; set; }
    public string LogFilterText { get; set; } = string.Empty;
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Core.Domain;
using WebLoadTester.Modules.UiScenario;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

public partial class UiScenarioSettingsViewModel : ObservableObject, ISettingsViewModel
{
    public ObservableCollection<UiScenarioStepViewModel> Steps { get; } = new();

    [ObservableProperty]
    private string targetUrl = "https://example.com";

    [ObservableProperty]
    private int totalRuns = 5;

    [ObservableProperty]
    private int concurrency = 1;

    [ObservableProperty]
    private bool headless = true;

    [ObservableProperty]
    private StepErrorPolicy errorPolicy = StepErrorPolicy.SkipStep;

    [ObservableProperty]
    private bool screenshotAfterScenario = true;

    public UiScenarioSettingsViewModel()
    {
        Steps.Add(new UiScenarioStepViewModel());
    }

    public object BuildSettings()
    {
        return new UiScenarioSettings
        {
            TargetUrl = TargetUrl,
            TotalRuns = TotalRuns,
            Concurrency = Concurrency,
            Headless = Headless,
            ErrorPolicy = ErrorPolicy,
            ScreenshotAfterScenario = ScreenshotAfterScenario,
            Steps = Steps.Select(s => s.ToStep()).ToList()
        };
    }
}

public partial class UiScenarioStepViewModel : ObservableObject
{
    [ObservableProperty]
    private UiStepAction action = UiStepAction.WaitForVisible;

    [ObservableProperty]
    private string selector = "body";

    [ObservableProperty]
    private string? text;

    [ObservableProperty]
    private int timeoutMs = 5000;

    public UiScenarioStep ToStep()
    {
        return new UiScenarioStep
        {
            Action = Action,
            Selector = Selector,
            Text = Text,
            TimeoutMs = TimeoutMs
        };
    }
}

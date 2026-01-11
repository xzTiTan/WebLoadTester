using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.UiScenario;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек UI-сценариев.
/// </summary>
public partial class UiScenarioSettingsViewModel : SettingsViewModelBase
{
    private readonly UiScenarioSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public UiScenarioSettingsViewModel(UiScenarioSettings settings)
    {
        _settings = settings;
        Steps = new ObservableCollection<UiStep>(settings.Steps);
        targetUrl = settings.TargetUrl;
        totalRuns = settings.TotalRuns;
        concurrency = settings.Concurrency;
        headless = settings.Headless;
        screenshotAfterScenario = settings.ScreenshotAfterScenario;
        Steps.CollectionChanged += (_, _) => _settings.Steps = Steps.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "UI Scenario";

    public ObservableCollection<UiStep> Steps { get; }

    [ObservableProperty]
    private string targetUrl = "https://example.com";

    [ObservableProperty]
    private int totalRuns = 1;

    [ObservableProperty]
    private int concurrency = 1;

    [ObservableProperty]
    private bool headless = true;

    [ObservableProperty]
    private bool screenshotAfterScenario = true;

    /// <summary>
    /// Синхронизирует URL сценария.
    /// </summary>
    partial void OnTargetUrlChanged(string value) => _settings.TargetUrl = value;
    /// <summary>
    /// Синхронизирует количество прогонов.
    /// </summary>
    partial void OnTotalRunsChanged(int value) => _settings.TotalRuns = value;
    /// <summary>
    /// Синхронизирует уровень конкурентности.
    /// </summary>
    partial void OnConcurrencyChanged(int value) => _settings.Concurrency = value;
    /// <summary>
    /// Синхронизирует режим headless.
    /// </summary>
    partial void OnHeadlessChanged(bool value) => _settings.Headless = value;
    /// <summary>
    /// Синхронизирует флаг сохранения скриншота после сценария.
    /// </summary>
    partial void OnScreenshotAfterScenarioChanged(bool value) => _settings.ScreenshotAfterScenario = value;
}

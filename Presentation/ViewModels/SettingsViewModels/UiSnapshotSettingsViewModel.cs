using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.UiSnapshot;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек UI-снимков.
/// </summary>
public partial class UiSnapshotSettingsViewModel : SettingsViewModelBase
{
    private readonly UiSnapshotSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public UiSnapshotSettingsViewModel(UiSnapshotSettings settings)
    {
        _settings = settings;
        Targets = new ObservableCollection<SnapshotTarget>(settings.Targets);
        waitUntil = settings.WaitUntil;
        timeoutSeconds = settings.TimeoutSeconds;
        screenshotFormat = settings.ScreenshotFormat;
        viewportWidth = settings.ViewportWidth;
        viewportHeight = settings.ViewportHeight;
        fullPage = settings.FullPage;
        Targets.CollectionChanged += (_, _) => _settings.Targets = Targets.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "UI снимки";
    public override void UpdateFrom(object settings)
    {
        if (settings is not UiSnapshotSettings s)
        {
            return;
        }

        Targets.Clear();
        foreach (var target in s.Targets)
        {
            Targets.Add(target);
        }

        WaitUntil = s.WaitUntil;
        TimeoutSeconds = s.TimeoutSeconds;
        ScreenshotFormat = s.ScreenshotFormat;
        ViewportWidth = s.ViewportWidth;
        ViewportHeight = s.ViewportHeight;
        FullPage = s.FullPage;
        _settings.Targets = Targets.ToList();
    }

    public ObservableCollection<SnapshotTarget> Targets { get; }

    public string[] WaitUntilOptions { get; } = { "load", "domcontentloaded", "networkidle" };
    public string[] ScreenshotFormatOptions { get; } = { "png" };

    [ObservableProperty]
    private SnapshotTarget? selectedTarget;

    [ObservableProperty]
    private string waitUntil = "load";

    [ObservableProperty]
    private int timeoutSeconds = 30;

    [ObservableProperty]
    private string screenshotFormat = "png";

    [ObservableProperty]
    private int? viewportWidth;

    [ObservableProperty]
    private int? viewportHeight;

    [ObservableProperty]
    private bool fullPage = true;

    /// <summary>
    /// Синхронизирует режим ожидания загрузки.
    /// </summary>
    partial void OnWaitUntilChanged(string value) => _settings.WaitUntil = value;
    /// <summary>
    /// Синхронизирует таймаут.
    /// </summary>
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;
    /// <summary>
    /// Синхронизирует формат скриншота.
    /// </summary>
    partial void OnScreenshotFormatChanged(string value) => _settings.ScreenshotFormat = value;
    /// <summary>
    /// Синхронизирует ширину viewport.
    /// </summary>
    partial void OnViewportWidthChanged(int? value) => _settings.ViewportWidth = value;
    /// <summary>
    /// Синхронизирует высоту viewport.
    /// </summary>
    partial void OnViewportHeightChanged(int? value) => _settings.ViewportHeight = value;
    /// <summary>
    /// Синхронизирует флаг полного снимка страницы.
    /// </summary>
    partial void OnFullPageChanged(bool value) => _settings.FullPage = value;

    [RelayCommand]
    private void AddTarget()
    {
        var target = new SnapshotTarget { Url = "https://example.com" };
        Targets.Add(target);
        SelectedTarget = target;
    }

    [RelayCommand]
    private void RemoveSelectedTarget()
    {
        if (SelectedTarget != null)
        {
            Targets.Remove(SelectedTarget);
        }
    }

    [RelayCommand]
    private void ClearTargets()
    {
        Targets.Clear();
    }
}

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
        baseUrl = settings.BaseUrl;
        Paths = new ObservableCollection<string>(settings.Paths);
        concurrency = settings.Concurrency;
        waitMode = settings.WaitMode;
        delayAfterLoadMs = settings.DelayAfterLoadMs;
        Paths.CollectionChanged += (_, _) => _settings.Paths = Paths.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "UI снимки";

    public ObservableCollection<string> Paths { get; }

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private string? selectedPath;

    [ObservableProperty]
    private int concurrency;

    [ObservableProperty]
    private string waitMode = "load";

    [ObservableProperty]
    private int delayAfterLoadMs;

    /// <summary>
    /// Синхронизирует базовый URL.
    /// </summary>
    partial void OnBaseUrlChanged(string value) => _settings.BaseUrl = value;
    /// <summary>
    /// Синхронизирует уровень конкурентности.
    /// </summary>
    partial void OnConcurrencyChanged(int value) => _settings.Concurrency = value;
    /// <summary>
    /// Синхронизирует режим ожидания загрузки.
    /// </summary>
    partial void OnWaitModeChanged(string value) => _settings.WaitMode = value;
    /// <summary>
    /// Синхронизирует задержку после загрузки.
    /// </summary>
    partial void OnDelayAfterLoadMsChanged(int value) => _settings.DelayAfterLoadMs = value;

    [RelayCommand]
    private void AddPath()
    {
        Paths.Add("/");
    }

    [RelayCommand]
    private void RemoveSelectedPath()
    {
        if (SelectedPath != null)
        {
            Paths.Remove(SelectedPath);
        }
    }
}

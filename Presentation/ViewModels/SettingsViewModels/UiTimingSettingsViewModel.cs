using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Modules.UiTiming;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек UI-таймингов.
/// </summary>
public partial class UiTimingSettingsViewModel : SettingsViewModelBase
{
    private readonly UiTimingSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public UiTimingSettingsViewModel(UiTimingSettings settings)
    {
        _settings = settings;
        baseUrl = settings.BaseUrl;
        Paths = new ObservableCollection<string>(settings.Paths);
        repeatsPerUrl = settings.RepeatsPerUrl;
        concurrency = settings.Concurrency;
        waitUntil = settings.WaitUntil;
        Paths.CollectionChanged += (_, _) => _settings.Paths = Paths.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "UI тайминги";

    public ObservableCollection<string> Paths { get; }

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private string? selectedPath;

    [ObservableProperty]
    private int repeatsPerUrl;

    [ObservableProperty]
    private int concurrency;

    [ObservableProperty]
    private string waitUntil = "load";

    /// <summary>
    /// Синхронизирует базовый URL.
    /// </summary>
    partial void OnBaseUrlChanged(string value) => _settings.BaseUrl = value;
    /// <summary>
    /// Синхронизирует количество повторов на URL.
    /// </summary>
    partial void OnRepeatsPerUrlChanged(int value) => _settings.RepeatsPerUrl = value;
    /// <summary>
    /// Синхронизирует уровень конкурентности.
    /// </summary>
    partial void OnConcurrencyChanged(int value) => _settings.Concurrency = value;
    /// <summary>
    /// Синхронизирует режим ожидания загрузки.
    /// </summary>
    partial void OnWaitUntilChanged(string value) => _settings.WaitUntil = value;

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

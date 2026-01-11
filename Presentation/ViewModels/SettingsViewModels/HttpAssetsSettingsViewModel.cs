using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpAssets;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

/// <summary>
/// ViewModel настроек проверки HTTP-ассетов.
/// </summary>
public partial class HttpAssetsSettingsViewModel : SettingsViewModelBase
{
    private readonly HttpAssetsSettings _settings;

    /// <summary>
    /// Инициализирует ViewModel и копирует настройки.
    /// </summary>
    public HttpAssetsSettingsViewModel(HttpAssetsSettings settings)
    {
        _settings = settings;
        Assets = new ObservableCollection<AssetItem>(settings.Assets);
        timeoutSeconds = settings.TimeoutSeconds;
        Assets.CollectionChanged += (_, _) => _settings.Assets = Assets.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "HTTP Assets";

    public ObservableCollection<AssetItem> Assets { get; }

    [ObservableProperty]
    private int timeoutSeconds;

    /// <summary>
    /// Синхронизирует таймаут запросов.
    /// </summary>
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;
}

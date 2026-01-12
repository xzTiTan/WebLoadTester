using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        baseUrl = settings.BaseUrl;
        Assets = new ObservableCollection<AssetItem>(settings.Assets);
        timeoutSeconds = settings.TimeoutSeconds;
        Assets.CollectionChanged += (_, _) => _settings.Assets = Assets.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "HTTP ассеты";

    public ObservableCollection<AssetItem> Assets { get; }

    [ObservableProperty]
    private string baseUrl = string.Empty;

    [ObservableProperty]
    private AssetItem? selectedAsset;

    [ObservableProperty]
    private int timeoutSeconds;

    /// <summary>
    /// Синхронизирует базовый URL.
    /// </summary>
    partial void OnBaseUrlChanged(string value) => _settings.BaseUrl = value;
    /// <summary>
    /// Синхронизирует таймаут запросов.
    /// </summary>
    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

    [RelayCommand]
    private void AddAsset()
    {
        var asset = new AssetItem { Path = "/" };
        Assets.Add(asset);
        SelectedAsset = asset;
    }

    [RelayCommand]
    private void RemoveSelectedAsset()
    {
        if (SelectedAsset != null)
        {
            Assets.Remove(SelectedAsset);
        }
    }
}

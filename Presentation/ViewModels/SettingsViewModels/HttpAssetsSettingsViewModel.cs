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

    public HttpAssetsSettingsViewModel(HttpAssetsSettings settings)
    {
        _settings = settings;
        foreach (var asset in settings.Assets)
        {
            asset.NormalizeLegacy();
        }

        Assets = new ObservableCollection<AssetItem>(settings.Assets);
        timeoutSeconds = settings.TimeoutSeconds;
        Assets.CollectionChanged += (_, _) => _settings.Assets = Assets.ToList();
    }

    public override object Settings => _settings;
    public override string Title => "HTTP ассеты";

    public override void UpdateFrom(object settings)
    {
        if (settings is not HttpAssetsSettings s)
        {
            return;
        }

        foreach (var asset in s.Assets)
        {
            asset.NormalizeLegacy();
        }

        TimeoutSeconds = s.TimeoutSeconds;
        Assets.Clear();
        foreach (var asset in s.Assets)
        {
            Assets.Add(asset);
        }

        _settings.Assets = Assets.ToList();
    }

    public ObservableCollection<AssetItem> Assets { get; }

    [ObservableProperty]
    private AssetItem? selectedAsset;

    partial void OnSelectedAssetChanged(AssetItem? value)
    {
        RemoveSelectedAssetCommand.NotifyCanExecuteChanged();
        DuplicateSelectedAssetCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private int timeoutSeconds;

    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

    [RelayCommand]
    private void AddAsset()
    {
        var asset = new AssetItem { Url = "https://example.com", Name = "Asset" };
        Assets.Add(asset);
        SelectedAsset = asset;
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelectedAsset))]
    private void DuplicateSelectedAsset()
    {
        if (SelectedAsset == null)
        {
            return;
        }

        var copy = new AssetItem
        {
            Url = SelectedAsset.Url,
            Name = string.IsNullOrWhiteSpace(SelectedAsset.Name) ? "Asset Copy" : $"{SelectedAsset.Name} Copy",
            ExpectedContentType = SelectedAsset.ExpectedContentType,
            MaxSizeKb = SelectedAsset.MaxSizeKb,
            MaxLatencyMs = SelectedAsset.MaxLatencyMs
        };

        var index = Assets.IndexOf(SelectedAsset);
        Assets.Insert(index + 1, copy);
        SelectedAsset = copy;
    }

    [RelayCommand(CanExecute = nameof(CanMutateSelectedAsset))]
    private void RemoveSelectedAsset()
    {
        if (SelectedAsset != null)
        {
            Assets.Remove(SelectedAsset);
        }
    }

    private bool CanMutateSelectedAsset() => SelectedAsset != null;
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpAssets;
using WebLoadTester.Presentation.ViewModels.Controls;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels.HttpAssets;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels;

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

        timeoutSeconds = settings.TimeoutSeconds;

        AssetRows = new ObservableCollection<AssetRowViewModel>(settings.Assets.Select(CreateRow));
        if (AssetRows.Count == 0)
        {
            AssetRows.Add(CreateRow(new AssetItem { Url = "https://example.com" }));
        }

        AssetsEditor = new RowListEditorViewModel();
        AssetsEditor.Configure(AddAssetInternal, RemoveAssetInternal, MoveAssetUpInternal, MoveAssetDownInternal, DuplicateAssetInternal, GetAssetErrors,
            selectedItemChanged: item => SelectedAssetRow = item as AssetRowViewModel);
        AssetsEditor.SetItems(AssetRows.Cast<object>());
        SelectedAssetRow = AssetRows.FirstOrDefault();
        SyncAssets();
    }

    public override object Settings => _settings;
    public override string Title => "HTTP ассеты";

    public ObservableCollection<AssetRowViewModel> AssetRows { get; }
    public RowListEditorViewModel AssetsEditor { get; }

    [ObservableProperty] private AssetRowViewModel? selectedAssetRow;
    [ObservableProperty] private int timeoutSeconds;

    partial void OnSelectedAssetRowChanged(AssetRowViewModel? value)
    {
        AssetsEditor.SelectedItem = value;
        AssetsEditor.RaiseCommandState();
        AssetsEditor.NotifyValidationChanged();
    }

    partial void OnTimeoutSecondsChanged(int value) => _settings.TimeoutSeconds = value;

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

        AssetRows.Clear();
        foreach (var asset in s.Assets)
        {
            AssetRows.Add(CreateRow(asset));
        }

        if (AssetRows.Count == 0)
        {
            AssetRows.Add(CreateRow(new AssetItem { Url = "https://example.com" }));
        }

        AssetsEditor.SetItems(AssetRows.Cast<object>());
        SelectedAssetRow = AssetRows.FirstOrDefault();
        SyncAssets();
    }

    private object? AddAssetInternal()
    {
        var row = CreateRow(new AssetItem { Url = "https://example.com" });
        var insertIndex = SelectedAssetRow != null ? AssetRows.IndexOf(SelectedAssetRow) + 1 : AssetRows.Count;
        if (insertIndex < 0 || insertIndex > AssetRows.Count)
        {
            insertIndex = AssetRows.Count;
        }

        AssetRows.Insert(insertIndex, row);
        SelectedAssetRow = row;
        SyncAssets();
        return row;
    }

    private void RemoveAssetInternal(object? selected)
    {
        if (selected is not AssetRowViewModel row)
        {
            return;
        }

        if (AssetRows.Count <= 1)
        {
            row.Clear();
            SyncAssets();
            return;
        }

        var index = AssetRows.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        AssetRows.RemoveAt(index);
        SelectedAssetRow = AssetRows.Count > 0 ? AssetRows[Math.Min(index, AssetRows.Count - 1)] : null;
        SyncAssets();
    }

    private void MoveAssetUpInternal(object? selected)
    {
        if (selected is not AssetRowViewModel row)
        {
            return;
        }

        var index = AssetRows.IndexOf(row);
        if (index > 0)
        {
            AssetRows.Move(index, index - 1);
            SelectedAssetRow = AssetRows[index - 1];
            SyncAssets();
        }
    }

    private void MoveAssetDownInternal(object? selected)
    {
        if (selected is not AssetRowViewModel row)
        {
            return;
        }

        var index = AssetRows.IndexOf(row);
        if (index >= 0 && index < AssetRows.Count - 1)
        {
            AssetRows.Move(index, index + 1);
            SelectedAssetRow = AssetRows[index + 1];
            SyncAssets();
        }
    }

    private void DuplicateAssetInternal(object? selected)
    {
        if (selected is not AssetRowViewModel row)
        {
            return;
        }

        var clone = row.Clone();
        var index = AssetRows.IndexOf(row);
        AssetRows.Insert(index + 1, clone);
        SelectedAssetRow = clone;
        SyncAssets();
    }

    private IEnumerable<string> GetAssetErrors() => AssetRows.Select(r => r.RowErrorText).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();

    private AssetRowViewModel CreateRow(AssetItem model)
    {
        var row = new AssetRowViewModel(model);
        row.PropertyChanged += (_, _) => SyncAssets();
        return row;
    }

    private void SyncAssets()
    {
        _settings.Assets = AssetRows.Select(r => r.Model).ToList();

        AssetsEditor.SetItems(AssetRows.Cast<object>());
        AssetsEditor.NotifyValidationChanged();
        AssetsEditor.RaiseCommandState();
    }
}

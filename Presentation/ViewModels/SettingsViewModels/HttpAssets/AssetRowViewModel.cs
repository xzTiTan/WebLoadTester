using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.HttpAssets;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels.HttpAssets;

public partial class AssetRowViewModel : ObservableObject
{
    public AssetRowViewModel(AssetItem model)
    {
        Model = model;
        name = model.Name ?? string.Empty;
        url = model.Url;
        expectedContentType = model.ExpectedContentType ?? string.Empty;
        maxSizeKb = model.MaxSizeKb;
        maxLatencyMs = model.MaxLatencyMs;
    }

    public AssetItem Model { get; }

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string url = string.Empty;
    [ObservableProperty] private string expectedContentType = string.Empty;
    [ObservableProperty] private int? maxSizeKb;
    [ObservableProperty] private int? maxLatencyMs;

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);
    public string RowErrorText => string.IsNullOrWhiteSpace(Url) ? "Asset: Url обязателен" : string.Empty;

    partial void OnNameChanged(string value) => Model.Name = string.IsNullOrWhiteSpace(value) ? null : value;
    partial void OnUrlChanged(string value)
    {
        Model.Url = value;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }
    partial void OnExpectedContentTypeChanged(string value) => Model.ExpectedContentType = string.IsNullOrWhiteSpace(value) ? null : value;
    partial void OnMaxSizeKbChanged(int? value) => Model.MaxSizeKb = value;
    partial void OnMaxLatencyMsChanged(int? value) => Model.MaxLatencyMs = value;

    public AssetRowViewModel Clone() => new(new AssetItem
    {
        Name = Name,
        Url = Url,
        ExpectedContentType = ExpectedContentType,
        MaxSizeKb = MaxSizeKb,
        MaxLatencyMs = MaxLatencyMs
    });

    public void Clear()
    {
        Name = string.Empty;
        Url = string.Empty;
        ExpectedContentType = string.Empty;
        MaxSizeKb = null;
        MaxLatencyMs = null;
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.UiSnapshot;
using WebLoadTester.Presentation.Common;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels.UiSnapshot;

public partial class SnapshotTargetRowViewModel : ObservableObject
{
    public SnapshotTargetRowViewModel(SnapshotTarget model)
    {
        Model = model;
        name = model.Name ?? string.Empty;
        url = model.Url;
        selector = model.Selector ?? string.Empty;
    }

    public SnapshotTarget Model { get; }

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string url = string.Empty;
    [ObservableProperty] private string selector = string.Empty;

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);
    public string RowErrorText => string.IsNullOrWhiteSpace(Url) ? "Цель: адрес обязателен" : string.Empty;

    partial void OnNameChanged(string value)
    {
        var normalized = InputValueGuard.NormalizeOptionalText(value);
        Model.Name = string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    partial void OnUrlChanged(string value)
    {
        var normalized = InputValueGuard.NormalizeOptionalText(value);
        if (normalized != value)
        {
            Url = normalized;
            return;
        }

        Model.Url = normalized;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    partial void OnSelectorChanged(string value)
    {
        var normalized = InputValueGuard.NormalizeOptionalText(value);
        Model.Selector = string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    public void RefreshComputed()
    {
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    public SnapshotTargetRowViewModel Clone() => new(new SnapshotTarget
    {
        Name = Name,
        Url = Url,
        Selector = Selector,
        Tag = Model.Tag
    });

    public void Clear()
    {
        Name = string.Empty;
        Url = string.Empty;
        Selector = string.Empty;
    }
}

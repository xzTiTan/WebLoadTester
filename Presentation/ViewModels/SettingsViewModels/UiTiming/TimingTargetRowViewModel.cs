using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.UiTiming;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels.UiTiming;

public partial class TimingTargetRowViewModel : ObservableObject
{
    public TimingTargetRowViewModel(TimingTarget model)
    {
        Model = model;
        url = model.Url;
    }

    public TimingTarget Model { get; }

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string url = string.Empty;

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);
    public string RowErrorText => string.IsNullOrWhiteSpace(Url) ? "Target: Url обязателен" : string.Empty;

    partial void OnUrlChanged(string value)
    {
        Model.Url = value;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    public TimingTargetRowViewModel Clone() => new(new TimingTarget { Url = Url }) { Name = Name };

    public void Clear()
    {
        Name = string.Empty;
        Url = string.Empty;
    }
}

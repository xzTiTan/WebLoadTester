using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.UiTiming;

namespace WebLoadTester.Presentation.ViewModels.SettingsViewModels.UiTiming;

public partial class TimingTargetRowViewModel : ObservableObject
{
    public TimingTargetRowViewModel(TimingTarget model)
    {
        Model = model;
        name = model.Name;
        url = model.Url;
        browserChannel = string.IsNullOrWhiteSpace(model.BrowserChannel) ? "chromium" : model.BrowserChannel;
        viewportWidth = model.ViewportWidth <= 0 ? 1366 : model.ViewportWidth;
        viewportHeight = model.ViewportHeight <= 0 ? 768 : model.ViewportHeight;
        userAgent = model.UserAgent;
        headless = model.Headless;
    }

    public TimingTarget Model { get; }

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string url = string.Empty;
    [ObservableProperty] private string browserChannel = "chromium";
    [ObservableProperty] private int viewportWidth = 1366;
    [ObservableProperty] private int viewportHeight = 768;
    [ObservableProperty] private string userAgent = string.Empty;
    [ObservableProperty] private bool? headless;

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);
    public string RowErrorText => string.IsNullOrWhiteSpace(Url)
        ? "Цель: адрес обязателен"
        : ViewportWidth < 320 || ViewportHeight < 240
            ? "Профиль: viewport должен быть не менее 320x240"
            : string.Empty;

    partial void OnNameChanged(string value) => Model.Name = value;
    partial void OnUrlChanged(string value)
    {
        Model.Url = value;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    partial void OnBrowserChannelChanged(string value) => Model.BrowserChannel = value;
    partial void OnViewportWidthChanged(int value)
    {
        Model.ViewportWidth = value;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    partial void OnViewportHeightChanged(int value)
    {
        Model.ViewportHeight = value;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    partial void OnUserAgentChanged(string value) => Model.UserAgent = value;
    partial void OnHeadlessChanged(bool? value) => Model.Headless = value;

    public TimingTargetRowViewModel Clone() => new(new TimingTarget
    {
        Name = Name,
        Url = Url,
        BrowserChannel = BrowserChannel,
        ViewportWidth = ViewportWidth,
        ViewportHeight = ViewportHeight,
        UserAgent = UserAgent,
        Headless = Headless
    });

    public void Clear()
    {
        Name = string.Empty;
        Url = string.Empty;
        BrowserChannel = "chromium";
        ViewportWidth = 1366;
        ViewportHeight = 768;
        UserAgent = string.Empty;
        Headless = null;
    }
}

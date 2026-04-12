using System.Collections.Generic;
using System.Linq;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Modules.UiTiming;
using WebLoadTester.Presentation.Common;

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

    public string BrowserDisplayName => "Chromium";
    public string BrowserDisplayHint => "Фиксированный движок модуля. Здесь сравниваются профили загрузки страницы, а не разные браузеры.";

    public IReadOnlyList<HeadlessModeChoice> HeadlessModeOptions { get; } =
    [
        new HeadlessModeChoice(null, "Наследовать из профиля запуска"),
        new HeadlessModeChoice(true, "Всегда headless"),
        new HeadlessModeChoice(false, "Открывать окно браузера")
    ];

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string url = string.Empty;
    [ObservableProperty] private string browserChannel = "chromium";
    [ObservableProperty] private int viewportWidth = 1366;
    [ObservableProperty] private int viewportHeight = 768;
    [ObservableProperty] private string userAgent = string.Empty;
    [ObservableProperty] private bool? headless;

    public HeadlessModeChoice SelectedHeadlessMode
    {
        get
        {
            var match = HeadlessModeOptions.FirstOrDefault(x => Nullable.Equals(x.Value, Headless));
            return string.IsNullOrWhiteSpace(match.Label) ? HeadlessModeOptions[0] : match;
        }
        set => Headless = value.Value;
    }

    public bool HasRowError => !string.IsNullOrWhiteSpace(RowErrorText);
    public string RowErrorText => string.IsNullOrWhiteSpace(Url)
        ? "Цель: адрес обязателен"
        : ViewportWidth < 320 || ViewportHeight < 240
            ? "Профиль: viewport должен быть не менее 320x240"
            : string.Empty;

    partial void OnNameChanged(string value) => Model.Name = InputValueGuard.NormalizeOptionalText(value);
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

    partial void OnBrowserChannelChanged(string value)
    {
        var normalized = InputValueGuard.NormalizeRequiredText(value, "chromium");
        if (normalized != value)
        {
            BrowserChannel = normalized;
            return;
        }

        Model.BrowserChannel = normalized;
    }

    partial void OnViewportWidthChanged(int value)
    {
        var normalized = InputValueGuard.NormalizeInt(value, 320, 1366);
        if (normalized != value)
        {
            ViewportWidth = normalized;
            return;
        }

        Model.ViewportWidth = normalized;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    partial void OnViewportHeightChanged(int value)
    {
        var normalized = InputValueGuard.NormalizeInt(value, 240, 768);
        if (normalized != value)
        {
            ViewportHeight = normalized;
            return;
        }

        Model.ViewportHeight = normalized;
        OnPropertyChanged(nameof(RowErrorText));
        OnPropertyChanged(nameof(HasRowError));
    }

    partial void OnUserAgentChanged(string value) => Model.UserAgent = InputValueGuard.NormalizeOptionalText(value);
    partial void OnHeadlessChanged(bool? value)
    {
        Model.Headless = value;
        OnPropertyChanged(nameof(SelectedHeadlessMode));
    }

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

public readonly record struct HeadlessModeChoice(bool? Value, string Label);

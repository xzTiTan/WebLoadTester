using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using WebLoadTester.Core.Domain;
using WebLoadTester.Presentation.Common;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class RunProfileViewModel : ObservableObject, IValidatable
{
    private readonly WebLoadTester.Presentation.ViewModels.RunProfileViewModel _legacy;

    public RunProfileViewModel(WebLoadTester.Presentation.ViewModels.RunProfileViewModel legacy)
    {
        _legacy = legacy;
        _legacy.PropertyChanged += OnLegacyPropertyChanged;
    }

    public WebLoadTester.Presentation.ViewModels.RunProfileViewModel Legacy => _legacy;

    [ObservableProperty]
    private bool isUiFamily = true;

    public Array ModeOptions => _legacy.ModeOptions;
    public Array ScreenshotsPolicyOptions => _legacy.ScreenshotsPolicyOptions;

    public RunMode Mode
    {
        get => _legacy.Mode;
        set => _legacy.Mode = value;
    }

    public int Iterations
    {
        get => _legacy.Iterations;
        set => _legacy.Iterations = value;
    }

    public int DurationSeconds
    {
        get => _legacy.DurationSeconds;
        set => _legacy.DurationSeconds = value;
    }

    public int Parallelism
    {
        get => _legacy.Parallelism;
        set => _legacy.Parallelism = value;
    }

    public int TimeoutSeconds
    {
        get => _legacy.TimeoutSeconds;
        set => _legacy.TimeoutSeconds = value;
    }

    public int PauseBetweenIterationsMs
    {
        get => _legacy.PauseBetweenIterationsMs;
        set => _legacy.PauseBetweenIterationsMs = value;
    }

    public bool Headless
    {
        get => _legacy.Headless;
        set => _legacy.Headless = value;
    }

    public ScreenshotsPolicy ScreenshotsPolicy
    {
        get => _legacy.ScreenshotsPolicy;
        set => _legacy.ScreenshotsPolicy = value;
    }

    public bool IsIterationsMode => _legacy.IsIterationsMode;
    public bool IsDurationMode => _legacy.IsDurationMode;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (Mode == RunMode.Iterations && Iterations < 1)
        {
            errors.Add("Итерации должны быть >= 1.");
        }

        if (Mode == RunMode.Duration && DurationSeconds < 1)
        {
            errors.Add("Длительность должна быть >= 1 сек.");
        }

        if (Parallelism < 1)
        {
            errors.Add("Параллелизм должен быть >= 1.");
        }

        if (TimeoutSeconds < 1)
        {
            errors.Add("Таймаут должен быть >= 1 сек.");
        }

        if (PauseBetweenIterationsMs < 0)
        {
            errors.Add("Пауза между итерациями должна быть >= 0 мс.");
        }

        return errors;
    }

    private void OnLegacyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(Iterations));
        OnPropertyChanged(nameof(DurationSeconds));
        OnPropertyChanged(nameof(Parallelism));
        OnPropertyChanged(nameof(TimeoutSeconds));
        OnPropertyChanged(nameof(PauseBetweenIterationsMs));
        OnPropertyChanged(nameof(Headless));
        OnPropertyChanged(nameof(ScreenshotsPolicy));
        OnPropertyChanged(nameof(IsIterationsMode));
        OnPropertyChanged(nameof(IsDurationMode));
        OnPropertyChanged(nameof(IsUiFamily));
    }
}

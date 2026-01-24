using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// ViewModel управления профилями запуска.
/// </summary>
public partial class RunProfileViewModel : ObservableObject
{
    private readonly IRunStore _runStore;

    public RunProfileViewModel(IRunStore runStore)
    {
        _runStore = runStore;
    }

    public ObservableCollection<RunProfile> Profiles { get; } = new();

    public Array ModeOptions { get; } = Enum.GetValues(typeof(RunMode));
    public Array ScreenshotsPolicyOptions { get; } = Enum.GetValues(typeof(ScreenshotsPolicy));

    [ObservableProperty]
    private RunProfile? selectedProfile;

    [ObservableProperty]
    private string profileName = "Default";

    [ObservableProperty]
    private int parallelism = 2;

    [ObservableProperty]
    private RunMode mode = RunMode.Iterations;

    [ObservableProperty]
    private int iterations = 10;

    [ObservableProperty]
    private int durationSeconds = 60;

    [ObservableProperty]
    private int timeoutSeconds = 30;

    [ObservableProperty]
    private bool headless = true;

    [ObservableProperty]
    private ScreenshotsPolicy screenshotsPolicy = ScreenshotsPolicy.OnError;

    [ObservableProperty]
    private bool htmlReportEnabled;

    [ObservableProperty]
    private bool telegramEnabled;

    [ObservableProperty]
    private bool preflightEnabled;

    public string WarningMessage =>
        Parallelism > 10 || DurationSeconds > 300
            ? "Высокая нагрузка: Parallelism > 10 или Duration > 5 минут."
            : string.Empty;

    partial void OnParallelismChanged(int value) => OnPropertyChanged(nameof(WarningMessage));
    partial void OnDurationSecondsChanged(int value) => OnPropertyChanged(nameof(WarningMessage));

    partial void OnSelectedProfileChanged(RunProfile? value)
    {
        if (value == null)
        {
            return;
        }

        ProfileName = value.Name;
        Parallelism = value.Parallelism;
        Mode = value.Mode;
        Iterations = value.Iterations;
        DurationSeconds = value.DurationSeconds;
        TimeoutSeconds = value.TimeoutSeconds;
        Headless = value.Headless;
        ScreenshotsPolicy = value.ScreenshotsPolicy;
        HtmlReportEnabled = value.HtmlReportEnabled;
        TelegramEnabled = value.TelegramEnabled;
        PreflightEnabled = value.PreflightEnabled;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Profiles.Clear();
        var profiles = await _runStore.GetRunProfilesAsync(CancellationToken.None);
        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        var profile = BuildProfileSnapshot();
        profile.Name = string.IsNullOrWhiteSpace(ProfileName) ? "Profile" : ProfileName;
        var saved = await _runStore.SaveRunProfileAsync(profile, CancellationToken.None);
        Profiles.Add(saved);
        SelectedProfile = saved;
    }

    public RunProfile BuildProfileSnapshot()
    {
        return new RunProfile
        {
            Id = Guid.Empty,
            Name = ProfileName,
            Parallelism = Parallelism,
            Mode = Mode,
            Iterations = Iterations,
            DurationSeconds = DurationSeconds,
            TimeoutSeconds = TimeoutSeconds,
            Headless = Headless,
            ScreenshotsPolicy = ScreenshotsPolicy,
            HtmlReportEnabled = HtmlReportEnabled,
            TelegramEnabled = TelegramEnabled,
            PreflightEnabled = PreflightEnabled
        };
    }
}

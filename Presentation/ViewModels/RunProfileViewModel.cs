using System;
using System.Collections.ObjectModel;
using System.Linq;
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

    [ObservableProperty]
    private bool isDeleteConfirmVisible;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public string WarningMessage =>
        Parallelism > 20 || DurationSeconds > 900 || Iterations > 2000
            ? "Высокая нагрузка: параллельность > 20, длительность > 15 минут или итерации > 2000."
            : string.Empty;

    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningMessage);
    public bool IsIterationsMode => Mode == RunMode.Iterations;
    public bool IsDurationMode => Mode == RunMode.Duration;

    partial void OnParallelismChanged(int value)
    {
        OnPropertyChanged(nameof(WarningMessage));
        OnPropertyChanged(nameof(HasWarning));
    }

    partial void OnDurationSecondsChanged(int value)
    {
        OnPropertyChanged(nameof(WarningMessage));
        OnPropertyChanged(nameof(HasWarning));
    }

    partial void OnIterationsChanged(int value)
    {
        OnPropertyChanged(nameof(WarningMessage));
        OnPropertyChanged(nameof(HasWarning));
    }
    partial void OnModeChanged(RunMode value)
    {
        OnPropertyChanged(nameof(IsIterationsMode));
        OnPropertyChanged(nameof(IsDurationMode));
    }

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
        IsDeleteConfirmVisible = false;
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
        var profile = BuildProfileSnapshot(SelectedProfile?.Id ?? Guid.Empty);
        profile.Name = string.IsNullOrWhiteSpace(ProfileName) ? "Profile" : ProfileName;
        var saved = await _runStore.SaveRunProfileAsync(profile, CancellationToken.None);
        await RefreshAsync();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == saved.Id) ?? saved;
        StatusMessage = "Профиль сохранён.";
    }

    [RelayCommand]
    private async Task SaveAsProfileAsync()
    {
        var name = string.IsNullOrWhiteSpace(ProfileName) ? "Profile" : ProfileName;
        if (Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "Профиль с таким именем уже существует. Укажите новое имя для сохранения.";
            return;
        }

        var profile = BuildProfileSnapshot(Guid.Empty);
        profile.Name = name;
        var saved = await _runStore.SaveRunProfileAsync(profile, CancellationToken.None);
        await RefreshAsync();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == saved.Id) ?? saved;
        StatusMessage = "Профиль сохранён как новый.";
    }

    [RelayCommand]
    private void RequestDeleteProfile()
    {
        if (SelectedProfile == null)
        {
            return;
        }

        IsDeleteConfirmVisible = true;
        StatusMessage = $"Удалить профиль \"{SelectedProfile.Name}\"?";
    }

    [RelayCommand]
    private async Task ConfirmDeleteProfileAsync()
    {
        if (SelectedProfile == null)
        {
            return;
        }

        await _runStore.DeleteRunProfileAsync(SelectedProfile.Id, CancellationToken.None);
        IsDeleteConfirmVisible = false;
        StatusMessage = "Профиль удалён.";
        SelectedProfile = null;
        await RefreshAsync();
    }

    [RelayCommand]
    private void CancelDeleteProfile()
    {
        IsDeleteConfirmVisible = false;
        StatusMessage = string.Empty;
    }

    public RunProfile BuildProfileSnapshot(Guid id)
    {
        return new RunProfile
        {
            Id = id,
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

    public RunParametersDto BuildRunParameters()
    {
        return new RunParametersDto
        {
            Mode = Mode,
            Iterations = Iterations,
            DurationSeconds = DurationSeconds,
            Parallelism = Parallelism,
            TimeoutSeconds = TimeoutSeconds,
            HtmlReportEnabled = HtmlReportEnabled,
            TelegramEnabled = TelegramEnabled,
            PreflightEnabled = PreflightEnabled,
            Headless = Headless,
            ScreenshotsPolicy = ScreenshotsPolicy
        };
    }

    public void UpdateFrom(RunParametersDto parameters)
    {
        Mode = parameters.Mode;
        Iterations = parameters.Iterations;
        DurationSeconds = parameters.DurationSeconds;
        Parallelism = parameters.Parallelism;
        TimeoutSeconds = parameters.TimeoutSeconds;
        HtmlReportEnabled = parameters.HtmlReportEnabled;
        TelegramEnabled = parameters.TelegramEnabled;
        PreflightEnabled = parameters.PreflightEnabled;
        Headless = parameters.Headless;
        ScreenshotsPolicy = parameters.ScreenshotsPolicy;
    }
}

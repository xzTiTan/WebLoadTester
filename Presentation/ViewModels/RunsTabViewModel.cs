using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.ViewModels;

public partial class RunsTabViewModel : ObservableObject
{
    private readonly IRunStore _runStore;
    private readonly string _runsRoot;
    private Func<string, Task> _repeatRun;
    private Func<bool>? _isRunningProvider;
    private CancellationTokenSource? _searchDebounceCts;
    private string? _pendingDeleteRunId;

    public RunsTabViewModel(IRunStore runStore, string runsRoot, Func<string, Task> repeatRun)
    {
        _runStore = runStore;
        _runsRoot = runsRoot;
        _repeatRun = repeatRun;

        StatusFilterOptions.Add("Все");
        StatusFilterOptions.Add("Успешно");
        StatusFilterOptions.Add("С ошибкой");
        StatusFilterOptions.Add("Остановлено");
        StatusFilterOptions.Add("Отменено");
        StatusFilterOptions.Add("Выполняется");

        PeriodOptions.Add("Сегодня");
        PeriodOptions.Add("7 дней");
        PeriodOptions.Add("30 дней");
        PeriodOptions.Add("Все");

        SelectedStatus = "Все";
        SelectedPeriod = "Все";
    }

    public ObservableCollection<TestRunSummary> AllRuns { get; } = new();
    public ObservableCollection<TestRunSummary> Runs { get; } = new();
    public ObservableCollection<string> ModuleFilterOptions { get; } = new();
    public ObservableCollection<string> StatusFilterOptions { get; } = new();
    public ObservableCollection<string> PeriodOptions { get; } = new();
    public ObservableCollection<ArtifactLinkItem> ArtifactLinks { get; } = new();
    public ObservableCollection<TopErrorItem> TopErrors { get; } = new();

    [ObservableProperty] private TestRunSummary? selectedRun;
    [ObservableProperty] private string? selectedModuleType;
    [ObservableProperty] private string selectedStatus = "Все";
    [ObservableProperty] private string selectedPeriod = "Все";
    [ObservableProperty] private bool onlyWithErrors;
    [ObservableProperty] private string? searchText;
    [ObservableProperty] private string userMessage = string.Empty;
    [ObservableProperty] private bool isDeleteConfirmVisible;
    [ObservableProperty] private string detailsSummary = string.Empty;
    [ObservableProperty] private string detailsProfile = string.Empty;
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private string repeatRunHint = string.Empty;

    public bool HasSelectedRun => SelectedRun != null;
    public bool HasRuns => Runs.Count > 0;
    public bool CanRepeatRun => RepeatRunCommand.CanExecute(null);
    public bool HasRepeatRunHint => !string.IsNullOrWhiteSpace(RepeatRunHint);

    public void SetModuleOptions(IEnumerable<string> moduleTypes)
    {
        ModuleFilterOptions.Clear();
        ModuleFilterOptions.Add("Все");
        foreach (var moduleType in moduleTypes.OrderBy(m => m))
        {
            ModuleFilterOptions.Add(moduleType);
        }

        SelectedModuleType ??= "Все";
    }


    public void ConfigureRepeatRun(Func<string, Task> repeatRun)
    {
        _repeatRun = repeatRun;
    }

    public void SetRunningStateProvider(Func<bool> isRunningProvider)
    {
        _isRunningProvider = isRunningProvider;
        IsRunning = _isRunningProvider();
    }

    public void RefreshRunningState()
    {
        if (_isRunningProvider != null)
        {
            IsRunning = _isRunningProvider();
        }
    }

    partial void OnSelectedModuleTypeChanged(string? value) => ApplyFilters();
    partial void OnSelectedStatusChanged(string value) => ApplyFilters();
    partial void OnSelectedPeriodChanged(string value) => ApplyFilters();
    partial void OnOnlyWithErrorsChanged(bool value) => ApplyFilters();
    partial void OnSelectedRunChanged(TestRunSummary? value)
    {
        OnPropertyChanged(nameof(HasSelectedRun));
        RepeatRunCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRepeatRun));
        OnPropertyChanged(nameof(HasRepeatRunHint));
        _ = Task.Run(LoadDetailsAsync);
        _ = Task.Run(UpdateRepeatRunAvailabilityAsync);
    }

    partial void OnIsRunningChanged(bool value)
    {
        RepeatRunCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRepeatRun));
        OnPropertyChanged(nameof(HasRepeatRunHint));
        if (value)
        {
            RepeatRunHint = "Остановите запуск, чтобы повторить.";
            return;
        }

        _ = Task.Run(UpdateRepeatRunAvailabilityAsync);
    }

    partial void OnSearchTextChanged(string? value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                if (!token.IsCancellationRequested)
                {
                    ApplyFilters();
                }
            }
            catch (TaskCanceledException)
            {
                // ignore debounce cancellation
            }
        }, token);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var previousRunId = SelectedRun?.RunId;
        IsDeleteConfirmVisible = false;
        _pendingDeleteRunId = null;

        AllRuns.Clear();
        var items = await _runStore.QueryRunsAsync(new RunQuery(), CancellationToken.None);
        foreach (var item in items)
        {
            AllRuns.Add(item);
        }

        ApplyFilters();

        if (!string.IsNullOrWhiteSpace(previousRunId))
        {
            SelectedRun = Runs.FirstOrDefault(r => r.RunId == previousRunId) ?? Runs.FirstOrDefault();
        }
        else
        {
            SelectedRun = Runs.FirstOrDefault();
        }

        UserMessage = string.Empty;
        await UpdateRepeatRunAvailabilityAsync();
    }

    [RelayCommand]
    private void OpenJson() => OpenPath(GetJsonPath(), "Файл report.json не найден.");

    [RelayCommand]
    private void OpenHtml() => OpenPath(GetHtmlPath(), "Файл report.html не найден.");

    [RelayCommand]
    private void OpenRunFolder()
    {
        if (SelectedRun == null)
        {
            return;
        }

        OpenPath(Path.Combine(_runsRoot, SelectedRun.RunId), "Папка прогона не найдена.");
    }

    [RelayCommand]
    private async Task CopyRunIdAsync()
    {
        if (SelectedRun == null)
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is IClipboard clipboard)
        {
            await clipboard.SetTextAsync(SelectedRun.RunId);
            UserMessage = "Идентификатор запуска скопирован в буфер обмена.";
        }
    }

    [RelayCommand]
    private async Task CopyRunPathAsync()
    {
        if (SelectedRun == null)
        {
            return;
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is IClipboard clipboard)
        {
            await clipboard.SetTextAsync(Path.Combine(_runsRoot, SelectedRun.RunId));
            UserMessage = "Путь прогона скопирован в буфер обмена.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteRepeatRun))]
    private async Task RepeatRunAsync()
    {
        if (SelectedRun == null)
        {
            return;
        }

        RefreshRunningState();
        if (IsRunning)
        {
            RepeatRunHint = "Остановите запуск, чтобы повторить.";
            return;
        }

        await _repeatRun(SelectedRun.RunId);
        UserMessage = $"Конфигурация загружена из прогона {SelectedRun.RunId}. Запуск не выполнялся.";
        await UpdateRepeatRunAvailabilityAsync();
    }

    private bool CanExecuteRepeatRun()
    {
        return HasSelectedRun && !IsRunning && string.IsNullOrWhiteSpace(RepeatRunHint);
    }

    [RelayCommand]
    private void RequestDeleteRun()
    {
        if (SelectedRun == null)
        {
            return;
        }

        _pendingDeleteRunId = SelectedRun.RunId;
        IsDeleteConfirmVisible = true;
        UserMessage = $"Удалить прогон {SelectedRun.RunId}? Будут удалены запись из БД и папка runs/{SelectedRun.RunId}.";
    }

    [RelayCommand]
    private async Task ConfirmDeleteRunAsync()
    {
        if (string.IsNullOrWhiteSpace(_pendingDeleteRunId))
        {
            return;
        }

        var runId = _pendingDeleteRunId;

        await _runStore.DeleteRunAsync(runId, CancellationToken.None);
        var runFolder = Path.Combine(_runsRoot, runId);
        try
        {
            if (Directory.Exists(runFolder))
            {
                Directory.Delete(runFolder, recursive: true);
            }

            UserMessage = "Прогон удалён.";
        }
        catch (Exception ex)
        {
            UserMessage = $"Запись БД удалена, но не удалось удалить папку: {ex.Message}";
        }

        IsDeleteConfirmVisible = false;
        _pendingDeleteRunId = null;
        await RefreshAsync();
    }

    [RelayCommand]
    private void CancelDeleteRun()
    {
        IsDeleteConfirmVisible = false;
        _pendingDeleteRunId = null;
        UserMessage = string.Empty;
    }

    [RelayCommand]
    private void OpenArtifact(ArtifactLinkItem? item)
    {
        if (item == null)
        {
            return;
        }

        OpenPath(item.FullPath, "Артефакт не найден.");
    }


    private async Task UpdateRepeatRunAvailabilityAsync()
    {
        RefreshRunningState();

        if (SelectedRun == null)
        {
            RepeatRunHint = string.Empty;
        }
        else if (IsRunning)
        {
            RepeatRunHint = "Остановите запуск, чтобы повторить.";
        }
        else
        {
            var reportPath = Path.Combine(_runsRoot, SelectedRun.RunId, "report.json");
            if (!File.Exists(reportPath))
            {
                RepeatRunHint = "report.json не найден для выбранного прогона.";
            }
            else
            {
                try
                {
                    var reportJson = await File.ReadAllTextAsync(reportPath);
                    RepeatRunHint = TryParseRepeatSnapshot(reportJson, out _, out var parseError)
                        ? string.Empty
                        : $"report.json повреждён: {parseError}";
                }
                catch (Exception ex)
                {
                    RepeatRunHint = $"Не удалось прочитать report.json: {ex.Message}";
                }
            }
        }

        RepeatRunCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRepeatRun));
        OnPropertyChanged(nameof(HasRepeatRunHint));
    }

    private void ApplyFilters()
    {
        var query = AllRuns.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedModuleType) && !string.Equals(SelectedModuleType, "Все", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.ModuleType, SelectedModuleType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedStatus) && !string.Equals(SelectedStatus, "Все", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.Status, MapStatusFilterToRunStatus(SelectedStatus), StringComparison.OrdinalIgnoreCase));
        }

        var utcNow = DateTimeOffset.UtcNow;
        query = SelectedPeriod switch
        {
            "Сегодня" => query.Where(r => r.StartedAt >= new DateTimeOffset(utcNow.Date, TimeSpan.Zero)),
            "7 дней" => query.Where(r => r.StartedAt >= utcNow.AddDays(-7)),
            "30 дней" => query.Where(r => r.StartedAt >= utcNow.AddDays(-30)),
            _ => query
        };

        if (OnlyWithErrors)
        {
            query = query.Where(r => r.FailedItems > 0);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var needle = SearchText.Trim();
            query = query.Where(r =>
                r.RunId.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || r.ModuleType.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || r.TestName.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.OrderByDescending(r => r.StartedAt).ToList();
        Runs.Clear();
        foreach (var run in filtered)
        {
            Runs.Add(run);
        }

        if (SelectedRun != null && Runs.All(r => r.RunId != SelectedRun.RunId))
        {
            SelectedRun = Runs.FirstOrDefault();
        }
    }

    private static string MapStatusFilterToRunStatus(string selectedStatus)
    {
        return selectedStatus switch
        {
            "Успешно" => "Success",
            "С ошибкой" => "Failed",
            "Остановлено" => "Stopped",
            "Отменено" => "Canceled",
            "Выполняется" => "Running",
            _ => selectedStatus
        };
    }

    private async Task LoadDetailsAsync()
    {
        ArtifactLinks.Clear();
        TopErrors.Clear();
        DetailsSummary = string.Empty;
        DetailsProfile = string.Empty;

        if (SelectedRun == null)
        {
            return;
        }

        var detail = await _runStore.GetRunDetailAsync(SelectedRun.RunId, CancellationToken.None);
        if (detail == null)
        {
            return;
        }

        DetailsSummary = $"Статус: {detail.Run.Status} · Длительность: {SelectedRun.DurationMs:F0} мс · Модуль: {detail.Run.ModuleType}";

        try
        {
            using var profileDoc = JsonDocument.Parse(detail.Run.ProfileSnapshotJson);
            var profile = profileDoc.RootElement;
            var timeoutSeconds = 30;
            if (profile.TryGetProperty("timeouts", out var timeouts)
                && timeouts.TryGetProperty("operationSeconds", out var operationSeconds)
                && operationSeconds.TryGetInt32(out var timeoutFromNested))
            {
                timeoutSeconds = timeoutFromNested;
            }
            else if (profile.TryGetProperty("timeoutSeconds", out var timeoutLegacy)
                     && timeoutLegacy.TryGetInt32(out var timeoutFromLegacy))
            {
                timeoutSeconds = timeoutFromLegacy;
            }

            DetailsProfile = $"Режим={profile.GetProperty("mode").GetString()} · Параллелизм={profile.GetProperty("parallelism").GetInt32()} · Таймаут={timeoutSeconds} · Пауза={(profile.TryGetProperty("pauseBetweenIterationsMs", out var pause) ? pause.GetInt32() : 0)}";
        }
        catch
        {
            DetailsProfile = "Профиль запуска недоступен.";
        }

        AddArtifactLink("report.json", GetJsonPath());
        AddArtifactLink("report.html", GetHtmlPath());
        AddArtifactLink("папка прогона", Path.Combine(_runsRoot, SelectedRun.RunId));
        AddArtifactLink("скриншоты", Path.Combine(_runsRoot, SelectedRun.RunId, "screenshots"));

        var topErrors = detail.Items
            .Where(i => !string.Equals(i.Status, "Success", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(i.ErrorMessage))
            .GroupBy(i => i.ErrorMessage!)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => new TopErrorItem(g.Key, g.Count()));

        foreach (var error in topErrors)
        {
            TopErrors.Add(error);
        }
    }

    private void AddArtifactLink(string name, string fullPath)
    {
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            ArtifactLinks.Add(new ArtifactLinkItem(name, fullPath));
        }
    }

    private string GetJsonPath()
    {
        return SelectedRun == null ? string.Empty : Path.Combine(_runsRoot, SelectedRun.RunId, "report.json");
    }

    private string GetHtmlPath()
    {
        return SelectedRun == null ? string.Empty : Path.Combine(_runsRoot, SelectedRun.RunId, "report.html");
    }

    private void OpenPath(string path, string notFoundMessage)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            UserMessage = notFoundMessage;
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        UserMessage = string.Empty;
    }

    public string GetSelectedRunJsonPath() => GetJsonPath();

    public static bool TryParseRepeatSnapshot(string reportJson, out RepeatRunSnapshot snapshot, out string error)
    {
        snapshot = new RepeatRunSnapshot(string.Empty, new RunProfile(), JsonSerializer.SerializeToElement(new { }), string.Empty);
        error = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(reportJson);
            var root = document.RootElement;
            var moduleId = root.GetProperty("moduleId").GetString();
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                error = "В report.json отсутствует moduleId.";
                return false;
            }

            if (!root.TryGetProperty("profile", out var profileElement))
            {
                error = "В report.json отсутствует profile.";
                return false;
            }

            var profile = ParseProfile(profileElement);
            var moduleSettings = root.TryGetProperty("moduleSettings", out var settingsElement)
                ? settingsElement.Clone()
                : JsonSerializer.SerializeToElement(new { });

            var finalName = root.TryGetProperty("finalName", out var finalNameElement)
                ? finalNameElement.GetString()
                : root.TryGetProperty("testName", out var testNameElement)
                    ? testNameElement.GetString()
                    : string.Empty;

            snapshot = new RepeatRunSnapshot(moduleId, profile, moduleSettings, finalName ?? string.Empty);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static RunProfile ParseProfile(JsonElement profile)
    {
        var timeout = 30;
        if (profile.TryGetProperty("timeouts", out var timeouts) && timeouts.TryGetProperty("operationSeconds", out var op) && op.TryGetInt32(out var nestedTimeout))
        {
            timeout = nestedTimeout;
        }
        else if (profile.TryGetProperty("timeoutSeconds", out var legacyTimeout) && legacyTimeout.TryGetInt32(out var legacyTimeoutValue))
        {
            timeout = legacyTimeoutValue;
        }

        var screenshots = profile.TryGetProperty("screenshotsPolicy", out var sp)
            && Enum.TryParse<ScreenshotsPolicy>(sp.GetString(), out var parsedScreenshots)
                ? parsedScreenshots
                : ScreenshotsPolicy.OnError;

        var mode = profile.TryGetProperty("mode", out var modeElement)
            && Enum.TryParse<RunMode>(modeElement.GetString(), true, out var parsedMode)
                ? parsedMode
                : RunMode.Iterations;

        return new RunProfile
        {
            Mode = mode,
            Parallelism = profile.TryGetProperty("parallelism", out var p) ? p.GetInt32() : 1,
            Iterations = profile.TryGetProperty("iterations", out var it) ? it.GetInt32() : 1,
            DurationSeconds = profile.TryGetProperty("durationSeconds", out var ds) ? ds.GetInt32() : 60,
            TimeoutSeconds = timeout,
            PauseBetweenIterationsMs = profile.TryGetProperty("pauseBetweenIterationsMs", out var pause) ? pause.GetInt32() : 0,
            Headless = profile.TryGetProperty("headless", out var h) && h.GetBoolean(),
            ScreenshotsPolicy = screenshots,
            HtmlReportEnabled = profile.TryGetProperty("htmlReportEnabled", out var html) && html.GetBoolean(),
            TelegramEnabled = profile.TryGetProperty("telegramEnabled", out var tg) && tg.GetBoolean(),
            PreflightEnabled = profile.TryGetProperty("preflightEnabled", out var pf) && pf.GetBoolean()
        };
    }
}

public sealed record ArtifactLinkItem(string Name, string FullPath);
public sealed record TopErrorItem(string Message, int Count);
public sealed record RepeatRunSnapshot(string ModuleId, RunProfile Profile, JsonElement ModuleSettings, string FinalName);

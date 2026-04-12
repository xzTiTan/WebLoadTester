using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.ViewModels;

public partial class RunsTabViewModel : ObservableObject
{
    private readonly IRunStore _runStore;
    private readonly string _runsRoot;
    private readonly SemaphoreSlim _refreshSync = new(1, 1);
    private Func<string, Task> _repeatRun;
    private Func<bool>? _isRunningProvider;
    private CancellationTokenSource? _searchDebounceCts;
    private string? _pendingDeleteRunId;
    private int _selectionStateVersion;
    private bool _hasValidSelection;
    private string? _validSelectedRunId;
    private readonly Dictionary<string, string> _moduleFilterToType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _moduleTypeToFilter = new(StringComparer.OrdinalIgnoreCase);

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

        ArtifactLinks.CollectionChanged += OnDetailCollectionChanged;
        TopErrors.CollectionChanged += OnDetailCollectionChanged;
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
    public bool HasValidSelection => _hasValidSelection;
    public bool HasRuns => Runs.Count > 0;
    public bool HasArtifactLinks => ArtifactLinks.Count > 0;
    public bool HasTopErrors => TopErrors.Count > 0;
    public bool CanRepeatRun => RepeatRunCommand.CanExecute(null);
    public bool HasRepeatRunHint => !string.IsNullOrWhiteSpace(RepeatRunHint);
    public string SelectedRunDisplayId => SelectedRun == null ? string.Empty : FormatRunId(SelectedRun.RunId);
    public string SelectedRunDisplayModule => SelectedRun == null ? string.Empty : ResolveModuleDisplayName(SelectedRun.ModuleType, SelectedRun.ModuleName);
    public string RunsSummaryText => HasRuns
        ? $"Показано запусков: {Runs.Count}"
        : SearchOrFilterActive()
            ? "По текущим фильтрам запуски не найдены."
            : "После первого запуска здесь появится журнал со всеми выполненными проверками.";

    public void SetModuleOptions(IEnumerable<string> moduleTypes)
    {
        _moduleFilterToType.Clear();
        _moduleTypeToFilter.Clear();
        ModuleFilterOptions.Clear();
        ModuleFilterOptions.Add("Все");
        foreach (var moduleType in moduleTypes.OrderBy(m => m))
        {
            var displayName = ResolveModuleDisplayName(moduleType, string.Empty);
            ModuleFilterOptions.Add(displayName);
            _moduleFilterToType[displayName] = moduleType;
            _moduleTypeToFilter[moduleType] = displayName;
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

    private void OnDetailCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasArtifactLinks));
        OnPropertyChanged(nameof(HasTopErrors));
    }

    partial void OnSelectedModuleTypeChanged(string? value) => ApplyFilters();
    partial void OnSelectedStatusChanged(string value) => ApplyFilters();
    partial void OnSelectedPeriodChanged(string value) => ApplyFilters();
    partial void OnOnlyWithErrorsChanged(bool value) => ApplyFilters();
    partial void OnSelectedRunChanged(TestRunSummary? value)
    {
        var validRun = UpdateValidSelectionState();
        NotifySelectionBindingsChanged();

        var version = Interlocked.Increment(ref _selectionStateVersion);
        _ = RefreshSelectionStateAsync(validRun, version);
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

        var version = Volatile.Read(ref _selectionStateVersion);
        _ = UpdateRepeatRunAvailabilityAsync(SelectedRun, version);
    }

    partial void OnSearchTextChanged(string? value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();

        _ = DebouncedApplyFiltersAsync(_searchDebounceCts.Token);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await _refreshSync.WaitAsync();
        try
        {
            var previousRunId = await Dispatcher.UIThread.InvokeAsync(() => SelectedRun?.RunId);
            var dbRuns = await _runStore.QueryRunsAsync(new RunQuery(), CancellationToken.None);
            var mergedRuns = await BuildMergedRunListAsync(dbRuns, CancellationToken.None);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsDeleteConfirmVisible = false;
                _pendingDeleteRunId = null;

                AllRuns.Clear();
                foreach (var item in mergedRuns)
                {
                    AllRuns.Add(item);
                }

                ApplyFilters();

                SelectedRun = !string.IsNullOrWhiteSpace(previousRunId)
                    ? Runs.FirstOrDefault(r => r.RunId == previousRunId)
                    : null;

                UserMessage = string.Empty;
            });

            var version = Volatile.Read(ref _selectionStateVersion);
            if (!HasValidSelection)
            {
                await RefreshSelectionStateAsync(null, version);
            }
        }
        finally
        {
            _refreshSync.Release();
        }
    }

    [RelayCommand]
    private void OpenJson()
    {
        if (OpenPath(GetJsonPath(), "Файл report.json не найден.", out var error))
        {
            UserMessage = string.Empty;
            return;
        }

        UserMessage = error;
    }

    [RelayCommand]
    private void OpenHtml()
    {
        var htmlPath = GetHtmlPath();
        if (OpenPath(htmlPath, "Файл report.html не найден.", out _))
        {
            UserMessage = string.Empty;
            return;
        }

        var jsonPath = GetJsonPath();
        if (OpenPath(jsonPath, "Файл report.json не найден.", out _))
        {
            UserMessage = "HTML-отчёт недоступен, открыт report.json.";
            return;
        }

        if (SelectedRun != null)
        {
            var runFolder = Path.Combine(_runsRoot, SelectedRun.RunId);
            if (OpenPath(runFolder, "Папка запуска не найдена.", out _))
            {
                UserMessage = "HTML и JSON недоступны, открыта папка запуска.";
                return;
            }
        }

        UserMessage = "Не удалось открыть HTML, JSON и папку запуска.";
    }

    [RelayCommand]
    private void OpenRunFolder()
    {
        if (SelectedRun == null)
        {
            return;
        }

        if (OpenPath(Path.Combine(_runsRoot, SelectedRun.RunId), "Папка запуска не найдена.", out var error))
        {
            UserMessage = string.Empty;
            return;
        }

        UserMessage = error;
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
            UserMessage = "Путь запуска скопирован в буфер обмена.";
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
        UserMessage = $"Конфигурация загружена из запуска {SelectedRun.RunId}. Запуск не выполнялся.";
        var version = Volatile.Read(ref _selectionStateVersion);
        await UpdateRepeatRunAvailabilityAsync(SelectedRun, version);
    }

    private bool CanExecuteRepeatRun()
    {
        return HasValidSelection && !IsRunning && string.IsNullOrWhiteSpace(RepeatRunHint);
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
        UserMessage = $"Удалить запуск {SelectedRun.RunId}? Будут удалены запись из БД и папка runs/{SelectedRun.RunId}.";
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

            UserMessage = "Запуск удалён.";
        }
        catch (Exception ex)
        {
            UserMessage = $"Запись в БД удалена, но не удалось удалить папку: {ex.Message}";
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

        if (OpenPath(item.FullPath, "Артефакт не найден.", out var error))
        {
            UserMessage = string.Empty;
            return;
        }

        UserMessage = error;
    }


    private async Task DebouncedApplyFiltersAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(ApplyFilters);
        }
        catch (TaskCanceledException)
        {
            // Ignore debounce cancellation.
        }
    }

    private Task RefreshSelectionStateAsync(TestRunSummary? run, int version)
    {
        return Task.WhenAll(
            LoadDetailsForRunAsync(run, version),
            UpdateRepeatRunAvailabilityAsync(run, version));
    }

    private async Task LoadDetailsForRunAsync(TestRunSummary? run, int version)
    {
        if (run == null)
        {
            if (!IsSelectionCurrent(null, version))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ArtifactLinks.Clear();
                TopErrors.Clear();
                DetailsSummary = string.Empty;
                DetailsProfile = string.Empty;
            });
            return;
        }

        var detail = await LoadRunDetailAsync(run);
        var summary = detail == null ? string.Empty : BuildDetailsSummary(detail, run);
        var profile = detail == null ? string.Empty : BuildDetailsProfile(detail.Run.ProfileSnapshotJson);
        var artifactLinks = detail == null ? Array.Empty<ArtifactLinkItem>() : BuildArtifactLinks(detail);
        var topErrors = detail == null
            ? Array.Empty<TopErrorItem>()
            : detail.Items
                .Where(i => !string.Equals(i.Status, "Success", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(i.ErrorMessage))
                .GroupBy(i => i.ErrorMessage!)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => new TopErrorItem(g.Key, g.Count()))
                .ToArray();

        if (!IsSelectionCurrent(run, version))
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ArtifactLinks.Clear();
            foreach (var artifactLink in artifactLinks)
            {
                ArtifactLinks.Add(artifactLink);
            }

            TopErrors.Clear();
            foreach (var error in topErrors)
            {
                TopErrors.Add(error);
            }

            DetailsSummary = summary;
            DetailsProfile = profile;
        });
    }

    private async Task<string> BuildRepeatRunHintAsync(TestRunSummary? run)
    {
        RefreshRunningState();
        if (run == null)
        {
            return string.Empty;
        }

        if (IsRunning)
        {
            return "Остановите запуск, чтобы повторить.";
        }

        var reportPath = Path.Combine(_runsRoot, run.RunId, "report.json");
        if (!File.Exists(reportPath))
        {
            return "report.json не найден для выбранного запуска.";
        }

        try
        {
            var reportJson = await File.ReadAllTextAsync(reportPath);
            return TryParseRepeatSnapshot(reportJson, out _, out var parseError)
                ? string.Empty
                : $"report.json повреждён: {parseError}";
        }
        catch (Exception ex)
        {
            return $"Не удалось прочитать report.json: {ex.Message}";
        }
    }

    private async Task<IReadOnlyList<TestRunSummary>> BuildMergedRunListAsync(IReadOnlyList<TestRunSummary> dbRuns, CancellationToken ct)
    {
        var merged = dbRuns.Select(CloneSummary).ToList();
        foreach (var run in merged)
        {
            ApplyFilesystemFlags(run);
        }

        var knownRunIds = new HashSet<string>(merged.Select(r => r.RunId), StringComparer.OrdinalIgnoreCase);
        var orphanRuns = await LoadOrphanRunsAsync(knownRunIds, ct);
        merged.AddRange(orphanRuns);

        return merged
            .OrderByDescending(r => r.StartedAt)
            .ThenByDescending(r => r.RunId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private Task<List<TestRunSummary>> LoadOrphanRunsAsync(HashSet<string> knownRunIds, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var result = new List<TestRunSummary>();
            if (!Directory.Exists(_runsRoot))
            {
                return result;
            }

            foreach (var runFolder in Directory.EnumerateDirectories(_runsRoot))
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var runId = Path.GetFileName(runFolder);
                if (string.IsNullOrWhiteSpace(runId) || knownRunIds.Contains(runId))
                {
                    continue;
                }

                var jsonPath = Path.Combine(runFolder, "report.json");
                if (!File.Exists(jsonPath))
                {
                    continue;
                }

                result.Add(BuildOrphanSummary(runId, runFolder, jsonPath));
            }

            return result;
        }, ct);
    }

    private TestRunSummary BuildOrphanSummary(string runId, string runFolder, string jsonPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            var root = document.RootElement;
            return new TestRunSummary
            {
                RunId = GetString(root, "runId") ?? runId,
                StartedAt = ParseDateTimeOffset(root, "startedAtUtc") ?? new DateTimeOffset(File.GetLastWriteTimeUtc(jsonPath)),
                TestName = GetString(root, "finalName", "testName") ?? runId,
                ModuleType = GetString(root, "moduleId") ?? "orphan",
                ModuleName = ResolveModuleDisplayName(GetString(root, "moduleId") ?? "orphan", GetString(root, "moduleName")),
                Status = GetString(root, "status") ?? "Unknown",
                DurationMs = GetNestedDouble(root, "summary", "durationMs") ?? 0,
                FailedItems = GetNestedInt32(root, "summary", "failedItems") ?? CountFailedItems(root),
                KeyMetrics = BuildKeyMetrics(GetNestedDouble(root, "summary", "averageMs"), GetNestedDouble(root, "summary", "p95Ms")),
                IsOrphan = true,
                IsReadOnly = true,
                RunFolderExists = Directory.Exists(runFolder),
                HasJsonReport = true,
                HasHtmlReport = File.Exists(Path.Combine(runFolder, "report.html"))
            };
        }
        catch
        {
            return new TestRunSummary
            {
                RunId = runId,
                StartedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(jsonPath)),
                TestName = $"Импортированный запуск {FormatRunId(runId)}",
                ModuleName = "Неизвестный модуль",
                ModuleType = "orphan",
                Status = "Unknown",
                DurationMs = 0,
                FailedItems = 0,
                KeyMetrics = string.Empty,
                IsOrphan = true,
                IsReadOnly = true,
                RunFolderExists = Directory.Exists(runFolder),
                HasJsonReport = true,
                HasHtmlReport = File.Exists(Path.Combine(runFolder, "report.html"))
            };
        }
    }

    private async Task<TestRunDetail?> LoadRunDetailAsync(TestRunSummary run)
    {
        var detail = await _runStore.GetRunDetailAsync(run.RunId, CancellationToken.None);
        if (detail != null)
        {
            detail.Run.IsOrphan = run.IsOrphan;
            detail.Run.IsReadOnly = run.IsReadOnly;
            detail.Run.RunFolderExists = run.RunFolderExists;
            detail.Run.HasJsonReport = run.HasJsonReport;
            detail.Run.HasHtmlReport = run.HasHtmlReport;
            detail.Artifacts = await BuildEffectiveArtifactsAsync(run.RunId, detail.Artifacts);
            return detail;
        }

        return await BuildOrphanDetailAsync(run);
    }

    private async Task<TestRunDetail> BuildOrphanDetailAsync(TestRunSummary run)
    {
        var runFolder = Path.Combine(_runsRoot, run.RunId);
        var reportPath = Path.Combine(runFolder, "report.json");
        var detail = new TestRunDetail
        {
            Run = new TestRun
            {
                RunId = run.RunId,
                TestCaseId = Guid.Empty,
                TestCaseVersion = 0,
                TestName = run.TestName,
                ModuleType = run.ModuleType,
                ModuleName = run.ModuleName,
                StartedAt = run.StartedAt,
                FinishedAt = null,
                Status = run.Status,
                SummaryJson = string.Empty,
                ProfileSnapshotJson = string.Empty,
                IsOrphan = true,
                IsReadOnly = true,
                RunFolderExists = run.RunFolderExists,
                HasJsonReport = run.HasJsonReport,
                HasHtmlReport = run.HasHtmlReport
            },
            Items = Array.Empty<RunItem>(),
            Artifacts = Array.Empty<ArtifactRecord>()
        };

        if (File.Exists(reportPath))
        {
            try
            {
                var reportJson = await File.ReadAllTextAsync(reportPath);
                using var document = JsonDocument.Parse(reportJson);
                var root = document.RootElement;

                detail.Run.RunId = GetString(root, "runId") ?? run.RunId;
                detail.Run.TestName = GetString(root, "finalName", "testName") ?? run.TestName;
                detail.Run.ModuleType = GetString(root, "moduleId") ?? run.ModuleType;
                detail.Run.ModuleName = ResolveModuleDisplayName(detail.Run.ModuleType, GetString(root, "moduleName"));
                detail.Run.StartedAt = ParseDateTimeOffset(root, "startedAtUtc") ?? run.StartedAt;
                detail.Run.FinishedAt = ParseDateTimeOffset(root, "finishedAtUtc");
                detail.Run.Status = GetString(root, "status") ?? run.Status;
                detail.Run.SummaryJson = root.TryGetProperty("summary", out var summaryElement) ? summaryElement.GetRawText() : string.Empty;
                detail.Run.ProfileSnapshotJson = root.TryGetProperty("profile", out var profileElement) ? profileElement.GetRawText() : string.Empty;
                detail.Items = ParseRunItems(root, run.RunId);
                detail.Artifacts = ParseArtifacts(root, run.RunId);
            }
            catch
            {
                // Keep minimal orphan detail for malformed report.json.
            }
        }

        detail.Artifacts = await BuildEffectiveArtifactsAsync(run.RunId, detail.Artifacts);
        return detail;
    }

    private async Task<IReadOnlyList<ArtifactRecord>> BuildEffectiveArtifactsAsync(string runId, IReadOnlyList<ArtifactRecord> seedArtifacts)
    {
        var runFolder = Path.Combine(_runsRoot, runId);
        var result = new Dictionary<string, ArtifactRecord>(StringComparer.OrdinalIgnoreCase);

        void AddArtifact(ArtifactRecord artifact)
        {
            if (string.IsNullOrWhiteSpace(artifact.RelativePath))
            {
                return;
            }

            var normalizedPath = artifact.RelativePath.Replace('\\', '/');
            result[normalizedPath] = new ArtifactRecord
            {
                Id = artifact.Id == Guid.Empty ? Guid.NewGuid() : artifact.Id,
                RunId = runId,
                ArtifactType = string.IsNullOrWhiteSpace(artifact.ArtifactType) ? GuessArtifactType(normalizedPath) : artifact.ArtifactType,
                RelativePath = artifact.RelativePath,
                CreatedAt = artifact.CreatedAt == default ? DateTimeOffset.UtcNow : artifact.CreatedAt
            };
        }

        foreach (var artifact in seedArtifacts)
        {
            AddArtifact(artifact);
        }

        var reportPath = Path.Combine(runFolder, "report.json");
        if (File.Exists(reportPath))
        {
            try
            {
                var reportJson = await File.ReadAllTextAsync(reportPath);
                using var document = JsonDocument.Parse(reportJson);
                foreach (var artifact in ParseArtifacts(document.RootElement, runId))
                {
                    AddArtifact(artifact);
                }
            }
            catch
            {
                // Ignore malformed report.json when collecting artifact links.
            }
        }

        if (Directory.Exists(runFolder))
        {
            foreach (var filePath in Directory.EnumerateFiles(runFolder, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(runFolder, filePath);
                AddArtifact(new ArtifactRecord
                {
                    Id = Guid.NewGuid(),
                    RunId = runId,
                    ArtifactType = GuessArtifactType(relativePath),
                    RelativePath = relativePath,
                    CreatedAt = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath))
                });
            }
        }

        return result.Values
            .OrderBy(artifact => ArtifactSortKey(artifact.RelativePath))
            .ThenBy(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<ArtifactLinkItem> BuildArtifactLinks(TestRunDetail detail)
    {
        var result = new List<ArtifactLinkItem>();
        var knownPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var runFolder = Path.Combine(_runsRoot, detail.Run.RunId);

        AddArtifactLink(result, knownPaths, "report.json", Path.Combine(runFolder, "report.json"));
        AddArtifactLink(result, knownPaths, "report.html", Path.Combine(runFolder, "report.html"));
        AddArtifactLink(result, knownPaths, "logs/run.log", Path.Combine(runFolder, "logs", "run.log"));
        AddArtifactLink(result, knownPaths, "Скриншоты", Path.Combine(runFolder, "screenshots"));
        AddArtifactLink(result, knownPaths, "Открыть папку запуска", runFolder);
        return result;
    }

    private static string GuessArtifactType(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        if (normalizedPath.Equals("report.json", StringComparison.OrdinalIgnoreCase))
        {
            return "JsonReport";
        }

        if (normalizedPath.Equals("report.html", StringComparison.OrdinalIgnoreCase))
        {
            return "HtmlReport";
        }

        if (normalizedPath.Equals("logs/run.log", StringComparison.OrdinalIgnoreCase))
        {
            return "Log";
        }

        if (normalizedPath.StartsWith("screenshots/", StringComparison.OrdinalIgnoreCase))
        {
            return "Screenshot";
        }

        return "File";
    }

    private static int ArtifactSortKey(string relativePath)
    {
        var normalizedPath = relativePath.Replace('\\', '/');
        return normalizedPath switch
        {
            "report.json" => 0,
            "report.html" => 1,
            "logs/run.log" => 2,
            _ when normalizedPath.StartsWith("screenshots/", StringComparison.OrdinalIgnoreCase) => 3,
            _ => 4
        };
    }

    private static void AddArtifactLink(ICollection<ArtifactLinkItem> items, ISet<string> knownPaths, string name, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || (!File.Exists(fullPath) && !Directory.Exists(fullPath)))
        {
            return;
        }

        var normalizedPath = fullPath.Replace('\\', '/');
        if (!knownPaths.Add(normalizedPath))
        {
            return;
        }

        items.Add(new ArtifactLinkItem(name, fullPath));
    }

    private static IReadOnlyList<RunItem> ParseRunItems(JsonElement root, string runId)
    {
        if (!root.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RunItem>();
        }

        var items = new List<RunItem>();
        foreach (var item in itemsElement.EnumerateArray())
        {
            items.Add(new RunItem
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                ItemType = GetString(item, "kind") ?? string.Empty,
                ItemKey = GetString(item, "name") ?? string.Empty,
                Status = item.TryGetProperty("ok", out var okElement) && okElement.ValueKind is JsonValueKind.True or JsonValueKind.False && okElement.GetBoolean()
                    ? "Success"
                    : "Failed",
                DurationMs = GetDouble(item, "durationMs") ?? 0,
                WorkerId = GetInt32(item, "workerId") ?? 0,
                Iteration = GetInt32(item, "iteration") ?? 0,
                ErrorMessage = GetString(item, "message"),
                ExtraJson = item.TryGetProperty("extra", out var extraElement) ? extraElement.GetRawText() : null
            });
        }

        return items;
    }

    private static IReadOnlyList<ArtifactRecord> ParseArtifacts(JsonElement root, string runId)
    {
        if (!root.TryGetProperty("artifacts", out var artifactsElement) || artifactsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ArtifactRecord>();
        }

        var artifacts = new List<ArtifactRecord>();
        foreach (var artifact in artifactsElement.EnumerateArray())
        {
            var relativePath = GetString(artifact, "relativePath");
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            artifacts.Add(new ArtifactRecord
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                ArtifactType = GetString(artifact, "type") ?? GuessArtifactType(relativePath),
                RelativePath = relativePath,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        return artifacts;
    }

    private static string BuildDetailsSummary(TestRunDetail detail, TestRunSummary run)
    {
        var durationMs = run.DurationMs > 0 ? run.DurationMs : ParseSummaryDuration(detail.Run.SummaryJson);
        var source = detail.Run.IsOrphan ? "Источник: импорт из папки запусков" : "Источник: журнал запусков";
        return $"Статус: {MapStatus(detail.Run.Status)} · Длительность: {FormatDuration(durationMs)} · Модуль: {ResolveModuleDisplayName(detail.Run.ModuleType, detail.Run.ModuleName)} · {source}";
    }

    private static double ParseSummaryDuration(string summaryJson)
    {
        if (string.IsNullOrWhiteSpace(summaryJson))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(summaryJson);
            return GetDouble(document.RootElement, "durationMs", "totalDurationMs") ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static TestRunSummary CloneSummary(TestRunSummary run)
    {
        return new TestRunSummary
        {
            RunId = run.RunId,
            StartedAt = run.StartedAt,
            TestName = run.TestName,
            ModuleName = ResolveModuleDisplayName(run.ModuleType, run.ModuleName),
            Status = run.Status,
            DurationMs = run.DurationMs,
            FailedItems = run.FailedItems,
            KeyMetrics = run.KeyMetrics,
            ModuleType = run.ModuleType,
            IsOrphan = run.IsOrphan,
            IsReadOnly = run.IsReadOnly,
            RunFolderExists = run.RunFolderExists,
            HasJsonReport = run.HasJsonReport,
            HasHtmlReport = run.HasHtmlReport
        };
    }

    private void ApplyFilesystemFlags(TestRunSummary run)
    {
        var runFolder = Path.Combine(_runsRoot, run.RunId);
        run.RunFolderExists = Directory.Exists(runFolder);
        run.HasJsonReport = File.Exists(Path.Combine(runFolder, "report.json"));
        run.HasHtmlReport = File.Exists(Path.Combine(runFolder, "report.html"));
    }

    private bool IsSelectionCurrent(TestRunSummary? run, int version)
    {
        return version == Volatile.Read(ref _selectionStateVersion)
               && string.Equals(_validSelectedRunId, run?.RunId, StringComparison.OrdinalIgnoreCase);
    }

    private async Task UpdateRepeatRunAvailabilityAsync(TestRunSummary? run, int version)
    {
        RefreshRunningState();
        var computedHint = await BuildRepeatRunHintAsync(run);
        if (!IsSelectionCurrent(run, version))
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RepeatRunHint = computedHint;
            RepeatRunCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanRepeatRun));
            OnPropertyChanged(nameof(HasRepeatRunHint));
        });
        return;

#if false
        if (SelectedRun == null)
        {
            hint = string.Empty;
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
                RepeatRunHint = "report.json не найден для выбранного запуска.";
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
#endif
    }

    private void ApplyFilters()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyFilters);
            return;
        }

        var query = AllRuns.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedModuleType) && !string.Equals(SelectedModuleType, "Все", StringComparison.OrdinalIgnoreCase))
        {
            var selectedModuleTypeValue = _moduleFilterToType.TryGetValue(SelectedModuleType, out var mappedModuleType)
                ? mappedModuleType
                : SelectedModuleType;
            query = query.Where(r => string.Equals(r.ModuleType, selectedModuleTypeValue, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedStatus) && !string.Equals(SelectedStatus, "Все", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.Status, MapStatusFilterToRunStatus(SelectedStatus), StringComparison.OrdinalIgnoreCase));
        }

        var localNow = DateTimeOffset.Now;
        var utcNow = localNow;
        var queryBeforePeriod = query;
        query = SelectedPeriod switch
        {
            "Сегодня" => query.Where(r => r.StartedAt >= new DateTimeOffset(utcNow.Date, TimeSpan.Zero)),
            "7 дней" => query.Where(r => r.StartedAt >= utcNow.AddDays(-7)),
            "30 дней" => query.Where(r => r.StartedAt >= utcNow.AddDays(-30)),
            _ => query
        };

        if (PeriodOptions.Count > 0 && string.Equals(SelectedPeriod, PeriodOptions[0], StringComparison.Ordinal))
        {
            query = queryBeforePeriod.Where(r => r.StartedAt.ToLocalTime().Date == localNow.Date);
        }

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
                || r.ModuleName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || r.Status.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || r.TestName.Contains(needle, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.OrderByDescending(r => r.StartedAt).ToList();
        Runs.Clear();
        foreach (var run in filtered)
        {
            Runs.Add(run);
        }

        OnPropertyChanged(nameof(HasRuns));
        OnPropertyChanged(nameof(RunsSummaryText));

        if (SelectedRun != null && Runs.All(r => r.RunId != SelectedRun.RunId))
        {
            SelectedRun = null;
        }

        UpdateValidSelectionState();
        NotifySelectionBindingsChanged();
    }

    private TestRunSummary? GetValidSelectedRun(TestRunSummary? candidate = null)
    {
        candidate ??= SelectedRun;
        if (candidate == null)
        {
            return null;
        }

        return Runs.FirstOrDefault(r => string.Equals(r.RunId, candidate.RunId, StringComparison.OrdinalIgnoreCase));
    }

    private TestRunSummary? UpdateValidSelectionState()
    {
        var validRun = GetValidSelectedRun();
        _validSelectedRunId = validRun?.RunId;
        _hasValidSelection = validRun != null;
        return validRun;
    }

    private void NotifySelectionBindingsChanged()
    {
        OnPropertyChanged(nameof(HasSelectedRun));
        OnPropertyChanged(nameof(HasValidSelection));
        OnPropertyChanged(nameof(RunsSummaryText));
        OnPropertyChanged(nameof(SelectedRunDisplayId));
        OnPropertyChanged(nameof(SelectedRunDisplayModule));
        RepeatRunCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRepeatRun));
        OnPropertyChanged(nameof(HasRepeatRunHint));
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

        DetailsSummary = $"Статус: {MapStatus(detail.Run.Status)} · Длительность: {FormatDuration(SelectedRun.DurationMs)} · Модуль: {ResolveModuleDisplayName(detail.Run.ModuleType, detail.Run.ModuleName)}";

        DetailsProfile = BuildDetailsProfile(detail.Run.ProfileSnapshotJson);

        AddArtifactLink("report.json", GetJsonPath());
        AddArtifactLink("report.html", GetHtmlPath());
        AddArtifactLink("Папка запуска", Path.Combine(_runsRoot, SelectedRun.RunId));
        AddArtifactLink("Скриншоты", Path.Combine(_runsRoot, SelectedRun.RunId, "screenshots"));

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

    private bool OpenPath(string path, string notFoundMessage, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            errorMessage = notFoundMessage;
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Не удалось открыть путь: {ex.Message}";
            return false;
        }
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

    private static string BuildDetailsProfile(string profileSnapshotJson)
    {
        if (TryDeserializeProfile(profileSnapshotJson, out var profile) && profile != null)
        {
            return FormatProfile(profile);
        }

        try
        {
            using var profileDoc = JsonDocument.Parse(profileSnapshotJson);
            var root = profileDoc.RootElement;
            var mode = GetString(root, "Mode", "mode") ?? RunMode.Iterations.ToString();
            var parallelism = GetInt32(root, "Parallelism", "parallelism") ?? 1;
            var timeoutSeconds =
                GetInt32(root, "TimeoutSeconds", "timeoutSeconds")
                ?? GetNestedInt32(root, new[] { "timeouts", "Timeouts" }, new[] { "operationSeconds", "OperationSeconds" })
                ?? 30;
            var pause = GetInt32(root, "PauseBetweenIterationsMs", "pauseBetweenIterationsMs") ?? 0;
            var htmlEnabled = GetBool(root, "HtmlReportEnabled", "htmlReportEnabled");
            var telegramEnabled = GetBool(root, "TelegramEnabled", "telegramEnabled");
            var preflightEnabled = GetBool(root, "PreflightEnabled", "preflightEnabled");

            return $"Режим: {(string.Equals(mode, nameof(RunMode.Duration), StringComparison.OrdinalIgnoreCase) ? "По длительности" : "По числу итераций")} · Параллелизм: {parallelism} · Таймаут: {timeoutSeconds} с · Пауза: {pause} мс · HTML: {(htmlEnabled ? "вкл" : "выкл")} · Telegram: {(telegramEnabled ? "вкл" : "выкл")} · Дымовой тест: {(preflightEnabled ? "вкл" : "выкл")}";
        }
        catch
        {
            return "Параметры запуска недоступны.";
        }
    }

    private static bool TryDeserializeProfile(string profileSnapshotJson, out RunProfile? profile)
    {
        try
        {
            profile = JsonSerializer.Deserialize<RunProfile>(profileSnapshotJson);
            return profile != null;
        }
        catch
        {
            profile = null;
            return false;
        }
    }

    private static string FormatProfile(RunProfile profile)
    {
        return $"Режим: {(profile.Mode == RunMode.Duration ? "По длительности" : "По числу итераций")} · Параллелизм: {profile.Parallelism} · Таймаут: {profile.TimeoutSeconds} с · Пауза: {profile.PauseBetweenIterationsMs} мс · HTML: {(profile.HtmlReportEnabled ? "вкл" : "выкл")} · Telegram: {(profile.TelegramEnabled ? "вкл" : "выкл")} · Дымовой тест: {(profile.PreflightEnabled ? "вкл" : "выкл")}";
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static int? GetInt32(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result))
            {
                return result;
            }
        }

        return null;
    }

    private static double? GetDouble(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.TryGetDouble(out var result))
            {
                return result;
            }
        }

        return null;
    }

    private static DateTimeOffset? ParseDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(value.GetString(), out var parsed)
            ? parsed
            : null;
    }

    private static double? GetNestedDouble(JsonElement element, string parentName, string childName)
    {
        if (!element.TryGetProperty(parentName, out var parent) || parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetDouble(parent, childName);
    }

    private static int? GetNestedInt32(JsonElement element, string parentName, string childName)
    {
        if (!element.TryGetProperty(parentName, out var parent) || parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetInt32(parent, childName);
    }

    private static string BuildKeyMetrics(double? averageMs, double? p95Ms)
    {
        return averageMs.GetValueOrDefault() > 0
            ? $"avg {averageMs.GetValueOrDefault():F1} ms, p95 {p95Ms.GetValueOrDefault():F1} ms"
            : string.Empty;
    }

    private static int CountFailedItems(JsonElement root)
    {
        if (!root.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var failed = 0;
        foreach (var item in itemsElement.EnumerateArray())
        {
            if (item.TryGetProperty("ok", out var okElement) &&
                okElement.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                !okElement.GetBoolean())
            {
                failed++;
            }
        }

        return failed;
    }

    private static bool GetBool(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) &&
                (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
            {
                return value.GetBoolean();
            }
        }

        return false;
    }

    private static int? GetNestedInt32(JsonElement element, string[] parentNames, string[] childNames)
    {
        foreach (var parentName in parentNames)
        {
            if (!element.TryGetProperty(parentName, out var parent) || parent.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = GetInt32(parent, childNames);
            if (value.HasValue)
            {
                return value;
            }
        }

        return null;
    }

    private static string ResolveModuleDisplayName(string? moduleType, string? fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(moduleType) && ModuleCatalog.TryGetByModuleId(moduleType, out var descriptor))
        {
            return descriptor.DisplayName;
        }

        return string.IsNullOrWhiteSpace(fallbackName)
            ? "Неизвестный модуль"
            : fallbackName.Trim();
    }

    public static string FormatRunId(string? runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return "—";
        }

        var normalized = runId.Trim();
        return normalized.Length <= 8 ? normalized : normalized[..8].ToUpperInvariant();
    }

    public static string FormatStartedAt(DateTimeOffset startedAt)
    {
        return startedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
    }

    public static string FormatDuration(double durationMs)
    {
        if (durationMs <= 0)
        {
            return "—";
        }

        if (durationMs < 1000)
        {
            return $"{durationMs:F0} мс";
        }

        var duration = TimeSpan.FromMilliseconds(durationMs);
        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes} мин {duration.Seconds} с";
        }

        return $"{duration.TotalSeconds:F1} с";
    }

    public static string MapStatus(string? status)
    {
        return status switch
        {
            "Success" => "Успешно",
            "Failed" => "С ошибкой",
            "Stopped" => "Остановлено",
            "Canceled" => "Отменено",
            "Running" => "Выполняется",
            _ => string.IsNullOrWhiteSpace(status) ? "Неизвестно" : status
        };
    }

    private bool SearchOrFilterActive()
    {
        return !string.IsNullOrWhiteSpace(SearchText)
               || !string.IsNullOrWhiteSpace(SelectedModuleType) && !string.Equals(SelectedModuleType, "Все", StringComparison.OrdinalIgnoreCase)
               || !string.IsNullOrWhiteSpace(SelectedStatus) && !string.Equals(SelectedStatus, "Все", StringComparison.OrdinalIgnoreCase)
               || !string.IsNullOrWhiteSpace(SelectedPeriod) && !string.Equals(SelectedPeriod, "Все", StringComparison.OrdinalIgnoreCase)
               || OnlyWithErrors;
    }
}

public sealed record ArtifactLinkItem(string Name, string FullPath);
public sealed record TopErrorItem(string Message, int Count);
public sealed record RepeatRunSnapshot(string ModuleId, RunProfile Profile, JsonElement ModuleSettings, string FinalName);


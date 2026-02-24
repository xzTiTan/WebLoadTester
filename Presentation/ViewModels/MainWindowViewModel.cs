using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Core.Services.ReportWriters;
using WebLoadTester.Infrastructure.Playwright;
using WebLoadTester.Infrastructure.Storage;
using WebLoadTester.Infrastructure.Telegram;
using WebLoadTester.Modules.Availability;
using WebLoadTester.Modules.HttpAssets;
using WebLoadTester.Modules.HttpFunctional;
using WebLoadTester.Modules.HttpPerformance;
using WebLoadTester.Modules.NetDiagnostics;
using WebLoadTester.Modules.Preflight;
using WebLoadTester.Modules.SecurityBaseline;
using WebLoadTester.Modules.UiScenario;
using WebLoadTester.Modules.UiSnapshot;
using WebLoadTester.Modules.UiTiming;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;
using WebLoadTester.Presentation.ViewModels.Tabs;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// Главная ViewModel приложения: управление запуском модулей и логами.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// Шина логов для отображения в UI.
    /// </summary>
    private readonly LogBus _logBus = new();
    /// <summary>
    /// Шина прогресса выполнения для обновления статуса.
    /// </summary>
    private readonly ProgressBus _progressBus = new();
    /// <summary>
    /// Хранилище артефактов запусков (отчёты, скриншоты).
    /// </summary>
    private readonly ArtifactStore _artifactStore;
    /// <summary>
    /// Хранилище тестов, профилей и прогонов.
    /// </summary>
    private readonly IRunStore _runStore;
    private readonly ITestCaseRepository _testCaseRepository;
    private readonly IModuleConfigService _moduleConfigService;
    /// <summary>
    /// Сервис настроек приложения.
    /// </summary>
    private readonly AppSettingsService _settingsService;
    /// <summary>
    /// Лимиты на выполнение тестов, применяемые при запуске.
    /// </summary>
    private readonly Limits _limits = new();
    /// <summary>
    /// Оркестратор запуска модулей и формирования отчётов.
    /// </summary>
    private readonly RunOrchestrator _orchestrator;

    /// <summary>
    /// Токен отмены текущего запуска.
    /// </summary>
    private CancellationTokenSource? _runCts;
    private TestRunNotificationContext? _activeTelegramContext;
    private bool _activeTelegramEnabled;
    private bool _isApplyingGuardedSelection;
    private int _lastConfirmedTabIndex;
    private ModuleItemViewModel? _lastUiModule;
    private ModuleItemViewModel? _lastHttpModule;
    private ModuleItemViewModel? _lastNetModule;
    private int _stopRequested;
    private readonly ITelegramRunNotifier _telegramRunNotifier;
    /// <summary>
    /// Источник завершения текущего прогона.
    /// </summary>
    private TaskCompletionSource<bool>? _runFinishedTcs;
    private Task CurrentRunFinishedTask => _runFinishedTcs?.Task ?? Task.CompletedTask;

    public event Action? RepeatRunPrepared;

    /// <summary>
    /// Инициализирует модули, вкладки и сервисы запуска.
    /// </summary>
    public MainWindowViewModel()
    {
        _settingsService = new AppSettingsService();
        PlaywrightFactory.ConfigureBrowsersPath(_settingsService.Settings.BrowsersDirectory);
        _runStore = new SqliteRunStore(_settingsService.Settings.DatabasePath);
        _testCaseRepository = (ITestCaseRepository)_runStore;
        _moduleConfigService = new ModuleConfigService(_testCaseRepository);
        _artifactStore = new ArtifactStore(_settingsService.Settings.RunsDirectory, Path.Combine(_settingsService.Settings.DataDirectory, "profiles"));
        RunProfile = new RunProfileViewModel(_runStore);

        var modules = new ITestModule[]
        {
            new UiScenarioModule(),
            new UiSnapshotModule(),
            new UiTimingModule(),
            new HttpFunctionalModule(),
            new HttpPerformanceModule(),
            new HttpAssetsModule(),
            new NetDiagnosticsModule(),
            new AvailabilityModule(),
            new SecurityBaselineModule(),
            new PreflightModule()
        };

        Registry = new ModuleRegistry(modules);
        UiFamily = new ModuleFamilyViewModel("UI тестирование", new ObservableCollection<ModuleItemViewModel>(
            Registry.GetByFamily(TestFamily.UiTesting).Select(CreateModuleItem)));
        HttpFamily = new ModuleFamilyViewModel("HTTP тестирование", new ObservableCollection<ModuleItemViewModel>(
            Registry.GetByFamily(TestFamily.HttpTesting).Select(CreateModuleItem)));
        NetFamily = new ModuleFamilyViewModel("Сеть и безопасность", new ObservableCollection<ModuleItemViewModel>(
            Registry.GetByFamily(TestFamily.NetSec).Select(CreateModuleItem)));
        UiFamily.PropertyChanged += OnFamilyPropertyChanged;
        HttpFamily.PropertyChanged += OnFamilyPropertyChanged;
        NetFamily.PropertyChanged += OnFamilyPropertyChanged;
        SubscribeValidationEvents(UiFamily);
        SubscribeValidationEvents(HttpFamily);
        SubscribeValidationEvents(NetFamily);
        RunProfile.PropertyChanged += OnRunProfilePropertyChanged;
        UpdateRunProfileModuleFamily();
        _telegramRunNotifier = new TelegramRunNotifier(_settingsService.Settings.Telegram, new TelegramClient());
        _telegramRunNotifier.StatusChanged += (_, _) => Dispatcher.UIThread.Post(UpdateTelegramStatus);
        TelegramSettings = new TelegramSettingsViewModel(_settingsService.Settings.Telegram, logInfo: _logBus.Info, logWarn: _logBus.Warn,
            onTestMessageResult: (success, error) => _telegramRunNotifier.ReportExternalResult(success, error));
        Settings = new SettingsWindowViewModel(_settingsService, TelegramSettings);
        RunsTab = new RunsTabViewModel(_runStore, _artifactStore.RunsRoot, RepeatRunFromReportAsync);
        RunsTab.SetModuleOptions(Registry.Modules.Select(m => m.Id));
        _lastConfirmedTabIndex = SelectedTabIndex;
        _lastUiModule = UiFamily.SelectedModule;
        _lastHttpModule = HttpFamily.SelectedModule;
        _lastNetModule = NetFamily.SelectedModule;

        _progressBus.ProgressChanged += OnProgressChanged;

        _orchestrator = new RunOrchestrator(new JsonReportWriter(_artifactStore), new HtmlReportWriter(_artifactStore), _runStore);
        _orchestrator.StageChanged += OnStageChanged;

        _ = Task.Run(ReadLogAsync);
        _ = Task.Run(InitializeAsync);
    }

    public ModuleRegistry Registry { get; }
    public ModuleFamilyViewModel UiFamily { get; }
    public ModuleFamilyViewModel HttpFamily { get; }
    public ModuleFamilyViewModel NetFamily { get; }
    public RunsTabViewModel RunsTab { get; }
    public TelegramSettingsViewModel TelegramSettings { get; }
    public RunProfileViewModel RunProfile { get; }
    public SettingsWindowViewModel Settings { get; }

    public ObservableCollection<string> LogEntries { get; } = new();
    public ObservableCollection<string> FilteredLogEntries { get; } = new();

    [ObservableProperty]
    private string statusText = "Статус: ожидание";

    [ObservableProperty]
    private string progressText = "Прогресс: 0/0";

    [ObservableProperty]
    private bool logOnlyErrors;

    [ObservableProperty]
    private bool logAutoScroll = true;

    [ObservableProperty]
    private bool isLogDrawerExpanded;

    [ObservableProperty]
    private string databaseStatus = "БД: проверка...";

    [ObservableProperty]
    private string telegramStatus = "Telegram: Выкл";

    [ObservableProperty]
    private bool isDatabaseOk;

    [ObservableProperty]
    private bool isTelegramConfigured;

    [ObservableProperty]
    private string telegramStatusTooltip = "Telegram выключен.";

    [ObservableProperty]
    private string runStage = "Ожидание";

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private bool isInstallingPlaywright;

    [ObservableProperty]
    private string playwrightInstallMessage = string.Empty;

    [ObservableProperty]
    private bool hasStartValidationErrors;

    [ObservableProperty]
    private string startValidationMessage = string.Empty;

    [ObservableProperty]
    private string loadedFromRunInfo = string.Empty;

    [ObservableProperty]
    private string requestedScrollToValidationKey = string.Empty;

    [ObservableProperty]
    private int requestedScrollNonce;

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    private bool _isProgressIndeterminate;
    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public string DatabaseStatusBadgeClass => IsDatabaseOk ? "badge ok" : "badge err";
    public string TelegramStatusBadgeClass => TelegramStatus.Contains("Ошибка", StringComparison.OrdinalIgnoreCase) ? "badge err" : TelegramStatus.Contains("Ок", StringComparison.OrdinalIgnoreCase) ? "badge ok" : "badge";
    public bool ShowRunHint => !IsRunning;
    public bool ShowPlaywrightInstallBanner => IsSelectedUiModule() && !PlaywrightFactory.HasBrowsersInstalled();
    public bool CanInstallPlaywright => !IsInstallingPlaywright && !PlaywrightFactory.IsInstalling;

    partial void OnIsDatabaseOkChanged(bool value) => OnPropertyChanged(nameof(DatabaseStatusBadgeClass));
    partial void OnIsTelegramConfiguredChanged(bool value) => OnPropertyChanged(nameof(TelegramStatusBadgeClass));

    /// <summary>
    /// Возвращает выбранный модуль в зависимости от активной вкладки.
    /// </summary>
    private ModuleItemViewModel? GetSelectedModule()
    {
        return SelectedTabIndex switch
        {
            0 => UiFamily.SelectedModule,
            1 => HttpFamily.SelectedModule,
            2 => NetFamily.SelectedModule,
            _ => null
        };
    }

    public ModuleItemViewModel? SelectedModule => GetSelectedModule();

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (_isApplyingGuardedSelection)
        {
            OnPropertyChanged(nameof(SelectedModule));
            OnPropertyChanged(nameof(ShowPlaywrightInstallBanner));
            UpdateRunProfileModuleFamily();
            ReevaluateStartAvailability();
            _lastConfirmedTabIndex = value;
            return;
        }

        var currentModuleConfig = GetModuleConfigByTab(_lastConfirmedTabIndex);
        if (value != _lastConfirmedTabIndex && currentModuleConfig != null)
        {
            var requestedTab = value;
            var allowed = currentModuleConfig.TryRequestLeave(async () =>
            {
                _isApplyingGuardedSelection = true;
                SelectedTabIndex = requestedTab;
                _isApplyingGuardedSelection = false;
            });

            if (!allowed)
            {
                _logBus.Info($"[NavGuard] Tab change blocked: module={SelectedModule?.Module.Id}, dirty={currentModuleConfig.IsDirty}");
                _isApplyingGuardedSelection = true;
                SelectedTabIndex = _lastConfirmedTabIndex;
                _isApplyingGuardedSelection = false;
                return;
            }
        }

        OnPropertyChanged(nameof(SelectedModule));
        OnPropertyChanged(nameof(ShowPlaywrightInstallBanner));
        UpdateRunProfileModuleFamily();
        ReevaluateStartAvailability();
        _lastConfirmedTabIndex = value;
    }

    /// <summary>
    /// Запускает выбранный модуль с учётом настроек и уведомлений.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        var moduleItem = GetSelectedModule();
        if (moduleItem == null)
        {
            return;
        }

        var validationErrors = GetStartValidationErrors(moduleItem);
        if (validationErrors.Count > 0)
        {
            IsRunning = false;
            StatusText = "Статус: ошибка валидации";
            moduleItem.ModuleConfig.ShowSubmitValidation();
            RunProfile.ShowSubmitValidation();
            RequestScrollToFirstValidation(moduleItem);
            moduleItem.ModuleConfig.StatusMessage = "Заполните обязательные поля: " + string.Join("; ", validationErrors);
            _logBus.Warn($"[Validation] {moduleItem.Module.Id}: {string.Join("; ", validationErrors)}");
            ReevaluateStartAvailability();
            return;
        }

        var testCase = await moduleItem.ModuleConfig.EnsureConfigForRunAsync();
        if (testCase == null)
        {
            return;
        }

        ClearLog();
        _runFinishedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IsRunning = true;
        StatusText = $"Статус: выполняется {moduleItem.DisplayName}";
        ProgressText = "Прогресс: 0/0";
        ProgressPercent = 0;
        IsProgressIndeterminate = true;
        RunStage = "Выполнение";

        _runCts = new CancellationTokenSource();
        Interlocked.Exchange(ref _stopRequested, 0);
        var runId = Guid.NewGuid().ToString("N");
        var profile = RunProfile.BuildProfileSnapshot(RunProfile.SelectedProfile?.Id ?? Guid.Empty);
        var notifier = CreateTelegramNotifier();
        var logSink = new CompositeLogSink(new ILogSink[]
        {
            _logBus,
            new FileLogSink(_artifactStore.GetLogPath(runId))
        });
        var ctx = new RunContext(logSink, _progressBus, _artifactStore, _limits, notifier,
            runId, profile, testCase.Name, testCase.Id, testCase.CurrentVersion, isStopRequested: () => Volatile.Read(ref _stopRequested) == 1);

        var notificationContext = new TestRunNotificationContext(
            runId,
            testCase.Name,
            moduleItem.Module.Id,
            profile,
            $"runs/{runId}",
            TimeProvider.System.GetUtcNow());
        _activeTelegramContext = notificationContext;
        _activeTelegramEnabled = profile.TelegramEnabled;

        await SendTelegramResultAsync(runId, () => _telegramRunNotifier.NotifyStartAsync(notificationContext, profile.TelegramEnabled, _runCts.Token));

        var finalProgressText = ProgressText;
        var finalStatusText = "Статус: ожидание";
        try
        {
            var preflight = CreatePreflightSettings(moduleItem.SettingsViewModel.Settings);
            var preflightModule = profile.PreflightEnabled ? Registry.Modules.FirstOrDefault(m => m.Id == "net.preflight") : null;
            var report = await _orchestrator.StartAsync(moduleItem.Module, moduleItem.SettingsViewModel.Settings, ctx, _runCts.Token,
                preflightModule, preflight);
            moduleItem.LastReport = report;
            finalProgressText = "Прогресс: завершено";
            finalStatusText = report.Status switch
            {
                TestStatus.Success => "Статус: успешно",
                TestStatus.Canceled => "Статус: отменено",
                TestStatus.Stopped => "Статус: остановлено",
                _ => "Статус: завершено с ошибками"
            };
            await SendTelegramResultAsync(runId, () => _telegramRunNotifier.NotifyCompletionAsync(report, profile.TelegramEnabled, _runCts.Token));
        }
        catch (OperationCanceledException)
        {
            finalProgressText = "Прогресс: отменено";
            finalStatusText = "Статус: отменено";
            await SendTelegramResultAsync(runId, () => _telegramRunNotifier.NotifyRunErrorAsync(notificationContext, "Операция отменена", profile.TelegramEnabled, CancellationToken.None));
        }
        catch (Exception ex)
        {
            finalProgressText = $"Прогресс: ошибка ({ex.Message})";
            finalStatusText = "Ошибка запуска: " + ex.Message;
            _logBus.Error($"Start failed: {ex}");
            await SendTelegramResultAsync(runId, () => _telegramRunNotifier.NotifyRunErrorAsync(notificationContext, ex.Message, profile.TelegramEnabled, _runCts.Token));
        }
        finally
        {
            await logSink.CompleteAsync();
            IsRunning = false;
            IsProgressIndeterminate = false;
            ProgressPercent = 0;
            ProgressText = finalProgressText;
            StatusText = finalStatusText;
            RunStage = finalStatusText.StartsWith("Ошибка", StringComparison.Ordinal) ? "Ошибка" : "Готово";
            _runCts?.Dispose();
            _runCts = null;
            Interlocked.Exchange(ref _stopRequested, 0);
            RunsTab.RefreshCommand.Execute(null);
            _activeTelegramContext = null;
            _activeTelegramEnabled = false;
            _runFinishedTcs?.TrySetResult(true);
        }
    }

    /// <summary>
    /// Останавливает текущий запуск.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        Interlocked.Exchange(ref _stopRequested, 1);
        StatusText = "Статус: мягкая остановка";
        RunStage = "Остановка";
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Cancel()
    {
        RequestCancel();
    }

    private void RequestCancel()
    {
        _runCts?.Cancel();
        StatusText = "Статус: отмена";
        RunStage = "Отменено";
    }

    /// <summary>
    /// Перезапускает выполнение выбранного модуля.
    /// </summary>
    [RelayCommand]
    private async Task RestartAsync()
    {
        if (IsRunning)
        {
            RequestCancel();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(CurrentRunFinishedTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                _logBus.Warn("Таймаут ожидания завершения прогона перед перезапуском.");
            }
        }

        ClearLog();
        await StartAsync();
    }

    [RelayCommand(CanExecute = nameof(CanInstallPlaywright))]
    private async Task InstallPlaywrightBrowsersAsync()
    {
        if (IsInstallingPlaywright || PlaywrightFactory.IsInstalling)
        {
            return;
        }

        IsInstallingPlaywright = true;
        PlaywrightInstallMessage = "Установка Chromium...";
        _logBus.Info($"[Playwright] Installing Chromium browser. Path: {PlaywrightFactory.GetBrowsersPath()}");

        try
        {
            var installed = await PlaywrightFactory.InstallChromiumAsync(CancellationToken.None, line => _logBus.Info($"[Playwright] {line}"));
            if (installed)
            {
                PlaywrightInstallMessage = "Chromium установлен.";
                _logBus.Info("[Playwright] Chromium installed successfully.");
            }
            else
            {
                PlaywrightInstallMessage = "Установка завершилась, но Chromium не обнаружен.";
                _logBus.Warn("[Playwright] Install finished, but Chromium was not detected in browsers path.");
            }
        }
        catch (Exception ex)
        {
            PlaywrightInstallMessage = $"Не удалось установить Chromium: {ex.Message}";
            _logBus.Error($"[Playwright] Install failed: {ex}");
        }
        finally
        {
            IsInstallingPlaywright = false;
            OnPropertyChanged(nameof(ShowPlaywrightInstallBanner));
            OnPropertyChanged(nameof(CanInstallPlaywright));
        }
    }

    /// <summary>
    /// Очищает список отображаемых логов.
    /// </summary>
    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
        FilteredLogEntries.Clear();
    }

    [RelayCommand]
    private async Task CopyLogAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var mainWindow = desktop.MainWindow;
        IClipboard? clipboard = mainWindow?.Clipboard;
        if (clipboard == null && mainWindow != null)
        {
            clipboard = TopLevel.GetTopLevel(mainWindow)?.Clipboard;
        }

        if (clipboard == null)
        {
            return;
        }

        var lines = LogOnlyErrors ? FilteredLogEntries : LogEntries;
        await clipboard.SetTextAsync(string.Join(Environment.NewLine, lines));
    }

    /// <summary>
    /// Проверяет, можно ли запускать модуль.
    /// </summary>
    private bool CanStart() => !IsRunning && !HasStartValidationErrors && GetSelectedModule() != null;
    /// <summary>
    /// Проверяет, можно ли остановить выполнение.
    /// </summary>
    private bool CanStop() => IsRunning;

    /// <summary>
    /// Обновляет доступность команд при смене состояния запуска.
    /// </summary>
    partial void OnIsRunningChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ShowRunHint));
        if (!value)
        {
            RunStage = "Ожидание";
        }

        ReevaluateStartAvailability();
    }

    partial void OnLogOnlyErrorsChanged(bool value)
    {
        RefreshFilteredLogs();
    }

    partial void OnIsInstallingPlaywrightChanged(bool value)
    {
        InstallPlaywrightBrowsersCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanInstallPlaywright));
    }

    /// <summary>
    /// Считывает логи из шины и добавляет их в UI.
    /// </summary>
    private async Task ReadLogAsync()
    {
        await foreach (var line in _logBus.ReadAllAsync())
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                AppendLogLine(LogEntries, line);
                if (ShouldShowLogLine(line))
                {
                    AppendLogLine(FilteredLogEntries, line);
                }
            });
        }
    }

    private void AppendLogLine(ObservableCollection<string> target, string line)
    {
        const int maxLines = 500;
        target.Add(line);
        while (target.Count > maxLines)
        {
            target.RemoveAt(0);
        }
    }

    private bool ShouldShowLogLine(string line)
    {
        return !LogOnlyErrors || line.Contains("ERROR", StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshFilteredLogs()
    {
        FilteredLogEntries.Clear();
        foreach (var line in LogEntries)
        {
            if (ShouldShowLogLine(line))
            {
                FilteredLogEntries.Add(line);
            }
        }
    }

    /// <summary>
    /// Обновляет прогресс и отправляет уведомления о ходе выполнения.
    /// </summary>
    private void OnProgressChanged(ProgressUpdate update)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (update.Total <= 0)
            {
                var suffix = string.IsNullOrWhiteSpace(update.Message) ? string.Empty : $" {update.Message}";
                ProgressText = $"Итераций: {Math.Max(update.Current, 0)} (duration){suffix}";
                IsProgressIndeterminate = true;
                ProgressPercent = 0;
            }
            else
            {
                ProgressText = $"Прогресс: {update.Current}/{update.Total} {update.Message}";
                IsProgressIndeterminate = false;
                ProgressPercent = (update.Current * 100.0) / update.Total;
            }
            if (!string.IsNullOrWhiteSpace(update.Message))
            {
                RunStage = update.Message;
            }
        });
        if (_runCts != null && _activeTelegramContext != null)
        {
            _ = SendTelegramResultAsync(_activeTelegramContext.RunId,
                () => _telegramRunNotifier.NotifyProgressAsync(_activeTelegramContext, update, _activeTelegramEnabled, _runCts.Token));
        }
    }

    private void OnStageChanged(object? sender, RunStageChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RunStage = e.Message ?? e.Stage.ToString();
        });
    }

    /// <summary>
    /// Создаёт уведомитель Telegram при наличии корректных настроек.
    /// </summary>
    private ITelegramNotifier? CreateTelegramNotifier()
    {
        if (!TelegramSettings.Settings.Enabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(TelegramSettings.Settings.BotToken) ||
            string.IsNullOrWhiteSpace(TelegramSettings.Settings.ChatId))
        {
            return null;
        }

        return new TelegramNotifier(TelegramSettings.Settings.BotToken, TelegramSettings.Settings.ChatId);
    }


    [RelayCommand]
    private void OpenTelegramSettings()
    {
        OpenSettings();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var selected = GetSelectedModule();
        if (selected != null)
        {
            var allowed = selected.ModuleConfig.TryRequestLeave(() =>
            {
                OpenSettingsWindow();
                return Task.CompletedTask;
            });

            if (!allowed)
            {
                return;
            }
        }

        OpenSettingsWindow();
    }

    private void OpenSettingsWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new WebLoadTester.Presentation.Views.SettingsWindow
            {
                DataContext = Settings
            };
            if (desktop.MainWindow is { } owner)
            {
                window.ShowDialog(owner);
            }
            else
            {
                window.Show();
            }
        }
    }

    [RelayCommand]
    private void OpenRunsFolder()
    {
        OpenPath(_artifactStore.RunsRoot);
    }

    [RelayCommand]
    private void OpenLatestJson()
    {
        var report = GetSelectedModule()?.LastReport;
        if (report == null || string.IsNullOrWhiteSpace(report.Artifacts.JsonPath))
        {
            return;
        }

        OpenPath(Path.Combine(_artifactStore.RunsRoot, report.RunId, report.Artifacts.JsonPath));
    }

    [RelayCommand]
    private void OpenLatestHtml()
    {
        var report = GetSelectedModule()?.LastReport;
        if (report == null || string.IsNullOrWhiteSpace(report.Artifacts.HtmlPath))
        {
            return;
        }

        OpenPath(Path.Combine(_artifactStore.RunsRoot, report.RunId, report.Artifacts.HtmlPath));
    }

    [RelayCommand]
    private void OpenLatestRunFolder()
    {
        var report = GetSelectedModule()?.LastReport;
        if (report == null)
        {
            return;
        }

        var path = Path.Combine(_artifactStore.RunsRoot, report.RunId);
        OpenPath(path);
    }

    [RelayCommand]
    private void OpenArtifact(ArtifactListItem? artifact)
    {
        if (artifact == null)
        {
            return;
        }

        var path = Path.Combine(_artifactStore.RunsRoot, artifact.RunId, artifact.RelativePath);
        OpenPath(path);
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _runStore.InitializeAsync(CancellationToken.None);
            Dispatcher.UIThread.Post(() => SetDatabaseStatus("БД: OK", true));
        }
        catch (Exception ex)
        {
            _logBus.Error($"DB init failed: {ex.Message}");
            Dispatcher.UIThread.Post(() => SetDatabaseStatus("БД: ошибка", false));
        }

        await RunProfile.RefreshCommand.ExecuteAsync(null);
        foreach (var module in Registry.Modules)
        {
            var item = FindModuleItem(module.Id);
            if (item != null)
            {
                await item.ModuleConfig.RefreshCommand.ExecuteAsync(null);
            }
        }

        await RunsTab.RefreshCommand.ExecuteAsync(null);
        UpdateTelegramStatus();
        TelegramSettings.PropertyChanged += (_, _) => UpdateTelegramStatus();
        ReevaluateStartAvailability();
    }

    private void UpdateTelegramStatus()
    {
        var status = _telegramRunNotifier.Status;
        TelegramStatus = status.State switch
        {
            TelegramIndicatorState.Off => "Telegram: Выкл",
            TelegramIndicatorState.Ok => "Telegram: Ок",
            _ => "Telegram: Ошибка"
        };

        IsTelegramConfigured = status.State == TelegramIndicatorState.Ok;
        TelegramStatusTooltip = status.State switch
        {
            TelegramIndicatorState.Off => "Telegram выключен.",
            TelegramIndicatorState.Ok => $"ChatId: {TelegramSettings.Settings.ChatId}. Уведомления готовы.",
            _ => $"ChatId: {TelegramSettings.Settings.ChatId}. {status.LastError ?? "Проверьте настройки Telegram."}"
        };
    }

    private void SetDatabaseStatus(string status, bool isOk)
    {
        DatabaseStatus = status;
        IsDatabaseOk = isOk;
    }

    private void OnFamilyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ModuleFamilyViewModel.SelectedModule) || sender is not ModuleFamilyViewModel family)
        {
            return;
        }

        var previous = ReferenceEquals(family, UiFamily)
            ? _lastUiModule
            : ReferenceEquals(family, HttpFamily)
                ? _lastHttpModule
                : _lastNetModule;

        if (_isApplyingGuardedSelection)
        {
            UpdateFamilySelectionSnapshot(family, family.SelectedModule);
            OnPropertyChanged(nameof(SelectedModule));
            OnPropertyChanged(nameof(ShowPlaywrightInstallBanner));
            UpdateRunProfileModuleFamily();
            ReevaluateStartAvailability();
            return;
        }

        var requested = family.SelectedModule;
        if (previous != null && requested != null && !ReferenceEquals(previous, requested))
        {
            var allowed = previous.ModuleConfig.TryRequestLeave(async () =>
            {
                _isApplyingGuardedSelection = true;
                family.SelectedModule = requested;
                _isApplyingGuardedSelection = false;
            });

            if (!allowed)
            {
                _isApplyingGuardedSelection = true;
                family.SelectedModule = previous;
                _isApplyingGuardedSelection = false;
                return;
            }
        }

        UpdateFamilySelectionSnapshot(family, requested);
        OnPropertyChanged(nameof(SelectedModule));
        OnPropertyChanged(nameof(ShowPlaywrightInstallBanner));
        UpdateRunProfileModuleFamily();
        ReevaluateStartAvailability();
    }

    private void UpdateFamilySelectionSnapshot(ModuleFamilyViewModel family, ModuleItemViewModel? selected)
    {
        if (ReferenceEquals(family, UiFamily))
        {
            _lastUiModule = selected;
        }
        else if (ReferenceEquals(family, HttpFamily))
        {
            _lastHttpModule = selected;
        }
        else if (ReferenceEquals(family, NetFamily))
        {
            _lastNetModule = selected;
        }
    }




    private ModuleConfigViewModel? GetModuleConfigByTab(int tabIndex)
    {
        return tabIndex switch
        {
            0 => UiFamily.SelectedModule?.ModuleConfig,
            1 => HttpFamily.SelectedModule?.ModuleConfig,
            2 => NetFamily.SelectedModule?.ModuleConfig,
            _ => null
        };
    }

    private bool IsSelectedUiModule()
    {
        var moduleId = SelectedModule?.Module.Id;
        return moduleId is "ui.scenario" or "ui.snapshot" or "ui.timing";
    }

    private void UpdateRunProfileModuleFamily()
    {
        var family = SelectedModule?.Module.Family ?? TestFamily.UiTesting;
        RunProfile.SetModuleFamily(family);
    }


    private void OnRunProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ReevaluateStartAvailability();
    }

    private void SubscribeValidationEvents(ModuleFamilyViewModel family)
    {
        foreach (var module in family.Modules)
        {
            module.SettingsViewModel.PropertyChanged += (_, _) => ReevaluateStartAvailability();
            module.ModuleConfig.PropertyChanged += (_, _) => ReevaluateStartAvailability();
        }
    }

    private IReadOnlyList<string> GetStartValidationErrors(ModuleItemViewModel moduleItem)
    {
        var errors = new List<string>();

        if (moduleItem.ModuleConfig.SelectedConfig == null)
        {
            if (string.IsNullOrWhiteSpace(moduleItem.ModuleConfig.UserName))
            {
                errors.Add("Укажите имя конфигурации перед запуском.");
            }
            else if (moduleItem.ModuleConfig.UserName.Any(char.IsWhiteSpace))
            {
                errors.Add("Имя конфигурации должно быть без пробелов.");
            }
        }

        var profile = RunProfile.BuildProfileSnapshot(RunProfile.SelectedProfile?.Id ?? Guid.Empty);
        errors.AddRange(_orchestrator.Validate(moduleItem.Module, moduleItem.SettingsViewModel.Settings, profile));
        return errors;
    }

    private void ReevaluateStartAvailability()
    {
        var moduleItem = GetSelectedModule();
        var errors = moduleItem == null ? new List<string> { "Не выбран модуль." } : GetStartValidationErrors(moduleItem).ToList();

        HasStartValidationErrors = errors.Count > 0;
        StartValidationMessage = errors.Count > 0 ? string.Join("; ", errors) : string.Empty;
        StartCommand.NotifyCanExecuteChanged();
    }

    private void RequestScrollToFirstValidation(ModuleItemViewModel moduleItem)
    {
        var key = moduleItem.ModuleConfig.GetFirstVisibleValidationKey()
                  ?? RunProfile.GetFirstVisibleValidationKey();

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        RequestedScrollToValidationKey = key;
        RequestedScrollNonce++;
    }


    private async Task SendTelegramResultAsync(string runId, Func<Task<TelegramSendResult>> action)
    {
        try
        {
            var result = await action();
            if (result.Success)
            {
                if (!string.Equals(result.Error, "Skipped", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(runId))
                {
                    await _runStore.AddTelegramNotificationAsync(new TelegramNotification
                    {
                        Id = Guid.NewGuid(),
                        RunId = runId,
                        SentAt = DateTimeOffset.Now,
                        Status = "Sent"
                    }, CancellationToken.None);
                }
            }
            else
            {
                _logBus.Warn($"Telegram failed: {result.Error}");
                if (!string.IsNullOrWhiteSpace(runId))
                {
                    await _runStore.AddTelegramNotificationAsync(new TelegramNotification
                    {
                        Id = Guid.NewGuid(),
                        RunId = runId,
                        SentAt = DateTimeOffset.Now,
                        Status = "Failed",
                        ErrorMessage = result.Error
                    }, CancellationToken.None);
                }
            }

            UpdateTelegramStatus();
        }
        catch (Exception ex)
        {
            _logBus.Warn($"Telegram failed: {ex.Message}");
            UpdateTelegramStatus();
        }
    }


    private ModuleItemViewModel? FindModuleItem(string moduleId)
    {
        return UiFamily.Modules.Concat(HttpFamily.Modules).Concat(NetFamily.Modules)
            .FirstOrDefault(item => item.Module.Id == moduleId);
    }

    public async Task RepeatRunFromReportAsync(string runId)
    {
        if (IsRunning)
        {
            _logBus.Warn("[Runs] Повтор запуска недоступен во время активного прогона.");
            return;
        }

        var reportPath = Path.Combine(_artifactStore.RunsRoot, runId, "report.json");
        if (File.Exists(reportPath))
        {
            var reportJson = await File.ReadAllTextAsync(reportPath);
            if (RunsTabViewModel.TryParseRepeatSnapshot(reportJson, out var snapshot, out var parseError))
            {
                var moduleItemFromReport = FindModuleItem(snapshot.ModuleId);
                if (moduleItemFromReport != null)
                {
                    var moduleSettings = System.Text.Json.JsonSerializer.Deserialize(snapshot.ModuleSettings.GetRawText(), moduleItemFromReport.Module.SettingsType);
                    if (moduleSettings != null)
                    {
                        moduleItemFromReport.SettingsViewModel.UpdateFrom(moduleSettings);
                    }

                    RunProfile.UpdateFrom(new RunParametersDto
                    {
                        Mode = snapshot.Profile.Mode,
                        Iterations = snapshot.Profile.Iterations,
                        DurationSeconds = snapshot.Profile.DurationSeconds,
                        Parallelism = snapshot.Profile.Parallelism,
                        TimeoutSeconds = snapshot.Profile.TimeoutSeconds,
                        PauseBetweenIterationsMs = snapshot.Profile.PauseBetweenIterationsMs,
                        HtmlReportEnabled = snapshot.Profile.HtmlReportEnabled,
                        TelegramEnabled = snapshot.Profile.TelegramEnabled,
                        PreflightEnabled = snapshot.Profile.PreflightEnabled,
                        Headless = snapshot.Profile.Headless,
                        ScreenshotsPolicy = snapshot.Profile.ScreenshotsPolicy
                    });

                    ApplyModuleSelection(moduleItemFromReport);

                    if (string.IsNullOrWhiteSpace(snapshot.FinalName))
                    {
                        _logBus.Warn($"[Runs] В report.json прогона {runId} отсутствует finalName/testName; имя повтора сформировано по fallback-правилу.");
                    }

                    var baseName = BuildRepeatUserName(snapshot.FinalName, moduleItemFromReport.Module.Id);
                    if (!string.IsNullOrWhiteSpace(baseName))
                    {
                        moduleItemFromReport.ModuleConfig.UserName = baseName + "_repeat";
                    }

                    moduleItemFromReport.ModuleConfig.Description = $"Повтор из прогона {runId}";

                    LoadedFromRunInfo = $"Загружено из прогона {runId}";
                    RepeatRunPrepared?.Invoke();
                    _logBus.Info($"[Runs] Конфигурация загружена из report.json прогона {runId}. Автозапуск не выполнялся.");
                    return;
                }

                _logBus.Warn($"[Runs] Модуль {snapshot.ModuleId} из report.json не найден в registry.");
            }
            else
            {
                _logBus.Warn($"[Runs] Не удалось разобрать report.json прогона {runId}: {parseError}");
            }
        }

        var detail = await _runStore.GetRunDetailAsync(runId, CancellationToken.None);
        if (detail == null)
        {
            _logBus.Warn($"[Runs] Не найден прогон {runId}.");
            return;
        }

        var moduleItem = FindModuleItem(detail.Run.ModuleType);
        if (moduleItem == null)
        {
            _logBus.Warn($"[Runs] Не найден модуль {detail.Run.ModuleType} для повтора прогона {runId}.");
            return;
        }

        var profile = System.Text.Json.JsonSerializer.Deserialize<RunProfile>(detail.Run.ProfileSnapshotJson);
        if (profile != null)
        {
            RunProfile.UpdateFrom(new RunParametersDto
            {
                Mode = profile.Mode,
                Iterations = profile.Iterations,
                DurationSeconds = profile.DurationSeconds,
                Parallelism = profile.Parallelism,
                TimeoutSeconds = profile.TimeoutSeconds,
                PauseBetweenIterationsMs = profile.PauseBetweenIterationsMs,
                HtmlReportEnabled = profile.HtmlReportEnabled,
                TelegramEnabled = profile.TelegramEnabled,
                PreflightEnabled = profile.PreflightEnabled,
                Headless = profile.Headless,
                ScreenshotsPolicy = profile.ScreenshotsPolicy
            });
        }

        ApplyModuleSelection(moduleItem);

        var fallbackName = BuildRepeatUserName(detail.Run.TestName, moduleItem.Module.Id);
        if (!string.IsNullOrWhiteSpace(fallbackName))
        {
            moduleItem.ModuleConfig.UserName = fallbackName + "_repeat";
        }

        moduleItem.ModuleConfig.Description = $"Повтор из прогона {runId}";

        LoadedFromRunInfo = $"Загружено из прогона {runId}";
        RepeatRunPrepared?.Invoke();
        _logBus.Info($"[Runs] Конфигурация загружена из snapshot БД прогона {runId}. Автозапуск не выполнялся.");
    }


    private void ApplyModuleSelection(ModuleItemViewModel moduleItem)
    {
        SelectedTabIndex = moduleItem.Module.Family switch
        {
            TestFamily.UiTesting => 0,
            TestFamily.HttpTesting => 1,
            TestFamily.NetSec => 2,
            _ => 0
        };

        switch (moduleItem.Module.Family)
        {
            case TestFamily.UiTesting:
                UiFamily.SelectedModule = moduleItem;
                break;
            case TestFamily.HttpTesting:
                HttpFamily.SelectedModule = moduleItem;
                break;
            case TestFamily.NetSec:
                NetFamily.SelectedModule = moduleItem;
                break;
        }
    }

    private static string BuildRepeatUserName(string? finalName, string moduleId)
    {
        if (string.IsNullOrWhiteSpace(finalName))
        {
            return string.Empty;
        }

        var suffix = "_" + ModuleCatalog.GetSuffix(moduleId);
        if (finalName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return finalName[..^suffix.Length];
        }

        var idx = finalName.LastIndexOf('_');
        if (idx > 0)
        {
            return finalName[..idx];
        }

        return finalName;
    }


    public UiLayoutState GetUiLayoutStateSnapshot()
    {
        var state = _settingsService.Settings.UiLayout ?? new UiLayoutState();
        return new UiLayoutState
        {
            LeftNavWidth = state.LeftNavWidth,
            DetailsWidth = state.DetailsWidth,
            IsDetailsVisible = state.IsDetailsVisible,
            IsLogExpanded = state.IsLogExpanded,
            IsLogOnlyErrors = state.IsLogOnlyErrors,
            LogFilterText = state.LogFilterText
        };
    }

    public async Task SaveUiLayoutStateAsync(UiLayoutState state)
    {
        _settingsService.Settings.UiLayout = state;
        await _settingsService.SaveAsync();
    }

    private static PreflightSettings? CreatePreflightSettings(object settings)
    {
        string? target = settings switch
        {
            UiScenarioSettings s => s.TargetUrl,
            UiSnapshotSettings s => s.Targets.FirstOrDefault()?.Url,
            UiTimingSettings s => s.Targets.FirstOrDefault()?.Url,
            HttpFunctionalSettings s => s.BaseUrl,
            HttpPerformanceSettings s => s.BaseUrl,
            HttpAssetsSettings s => s.Assets.FirstOrDefault()?.Url,
            NetDiagnosticsSettings s => $"https://{s.Hostname}",
            AvailabilitySettings s => s.Target,
            SecurityBaselineSettings s => s.Url,
            PreflightSettings s => s.Target,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        return new PreflightSettings
        {
            Target = target,
            CheckDns = true,
            CheckTcp = true,
            CheckTls = true,
            CheckHttp = true
        };
    }

    private static void OpenPath(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    /// <summary>
    /// Создаёт ViewModel элемента модуля с соответствующими настройками.
    /// </summary>
    private ModuleItemViewModel CreateModuleItem(ITestModule module)
    {
        var settings = module.CreateDefaultSettings();
        SettingsViewModelBase settingsVm = module switch
        {
            UiScenarioModule => new UiScenarioSettingsViewModel((UiScenarioSettings)settings),
            UiSnapshotModule => new UiSnapshotSettingsViewModel((UiSnapshotSettings)settings),
            UiTimingModule => new UiTimingSettingsViewModel((UiTimingSettings)settings),
            HttpFunctionalModule => new HttpFunctionalSettingsViewModel((HttpFunctionalSettings)settings),
            HttpPerformanceModule => new HttpPerformanceSettingsViewModel((HttpPerformanceSettings)settings),
            HttpAssetsModule => new HttpAssetsSettingsViewModel((HttpAssetsSettings)settings),
            NetDiagnosticsModule => new NetDiagnosticsSettingsViewModel((NetDiagnosticsSettings)settings),
            AvailabilityModule => new AvailabilitySettingsViewModel((AvailabilitySettings)settings),
            SecurityBaselineModule => new SecurityBaselineSettingsViewModel((SecurityBaselineSettings)settings),
            PreflightModule => new PreflightSettingsViewModel((PreflightSettings)settings),
            _ => throw new NotSupportedException("Unknown module")
        };

        var moduleConfig = new ModuleConfigViewModel(_moduleConfigService, _testCaseRepository, module, settingsVm, RunProfile);
        return new ModuleItemViewModel(module, settingsVm, moduleConfig);
    }
}

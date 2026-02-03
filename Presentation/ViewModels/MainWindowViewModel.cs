using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Core.Services.ReportWriters;
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
    /// <summary>
    /// Политика Telegram-уведомлений для текущего запуска.
    /// </summary>
    private TelegramPolicy? _telegramPolicy;

    /// <summary>
    /// Инициализирует модули, вкладки и сервисы запуска.
    /// </summary>
    public MainWindowViewModel()
    {
        _settingsService = new AppSettingsService();
        _runStore = new SqliteRunStore(_settingsService.Settings.DatabasePath);
        _artifactStore = new ArtifactStore(_settingsService.Settings.RunsDirectory, _settingsService.Settings.ProfilesDirectory);

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

        RunProfile = new RunProfileViewModel(_runStore);
        TelegramSettings = new TelegramSettingsViewModel(new TelegramSettings());
        Settings = new SettingsWindowViewModel(_settingsService, TelegramSettings);
        RunsTab = new RunsTabViewModel(_runStore, _artifactStore.RunsRoot, RepeatRunAsync);
        RunsTab.SetModuleOptions(Registry.Modules.Select(m => m.Id));

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
    private string databaseStatus = "БД: проверка...";

    [ObservableProperty]
    private string telegramStatus = "Telegram: не настроен";

    [ObservableProperty]
    private bool isDatabaseOk;

    [ObservableProperty]
    private bool isTelegramConfigured;

    [ObservableProperty]
    private string runStage = "Ожидание";

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private int selectedTabIndex;

    public string DatabaseStatusBadgeClass => IsDatabaseOk ? "badge ok" : "badge err";
    public string TelegramStatusBadgeClass => IsTelegramConfigured ? "badge ok" : "badge warn";

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

        IsRunning = true;
        StatusText = $"Статус: выполняется {moduleItem.DisplayName}";
        ProgressText = "Прогресс: 0/0";
        RunStage = "Выполнение";

        _runCts = new CancellationTokenSource();
        var profile = RunProfile.BuildProfileSnapshot(RunProfile.SelectedProfile?.Id ?? Guid.Empty);
        var runId = Guid.NewGuid().ToString("N");
        var notifier = profile.TelegramEnabled ? CreateTelegramNotifier() : null;
        _telegramPolicy = new TelegramPolicy(notifier, TelegramSettings.Settings);

        var testCase = await moduleItem.TestLibrary.EnsureTestCaseAsync(_runCts.Token);
        var logSink = new CompositeLogSink(new ILogSink[]
        {
            _logBus,
            new FileLogSink(_artifactStore.GetLogPath(runId))
        });
        var ctx = new RunContext(logSink, _progressBus, _artifactStore, _limits, notifier,
            runId, profile, testCase.Name, testCase.Id, testCase.CurrentVersion);

        if (_telegramPolicy.IsEnabled)
        {
            await SendTelegramAsync(runId, () => _telegramPolicy.NotifyStartAsync(moduleItem.DisplayName, runId, _runCts.Token));
        }

        try
        {
            var preflight = CreatePreflightSettings(moduleItem.SettingsViewModel.Settings);
            var preflightModule = profile.PreflightEnabled ? Registry.Modules.FirstOrDefault(m => m.Id == "net.preflight") : null;
            var report = await _orchestrator.StartAsync(moduleItem.Module, moduleItem.SettingsViewModel.Settings, ctx, _runCts.Token,
                preflightModule, preflight);
            moduleItem.LastReport = report;
            if (_telegramPolicy.IsEnabled)
            {
                await SendTelegramAsync(runId, () => _telegramPolicy.NotifyFinishAsync(report, _runCts.Token));
            }
        }
        catch (Exception ex)
        {
            await SendTelegramAsync(runId, () => _telegramPolicy.NotifyErrorAsync(ex.Message, _runCts.Token));
        }
        finally
        {
            await logSink.CompleteAsync();
            IsRunning = false;
            StatusText = "Статус: ожидание";
            RunStage = "Готово";
            RunsTab.RefreshCommand.Execute(null);
            _telegramPolicy = null;
        }
    }

    /// <summary>
    /// Останавливает текущий запуск.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        CancelRun();
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Cancel()
    {
        CancelRun();
    }

    private void CancelRun()
    {
        _runCts?.Cancel();
        StatusText = "Статус: остановка";
        RunStage = "Отменено";
    }

    /// <summary>
    /// Перезапускает выполнение выбранного модуля.
    /// </summary>
    [RelayCommand]
    private async Task RestartAsync()
    {
        Stop();
        await StartAsync();
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

    /// <summary>
    /// Проверяет, можно ли запускать модуль.
    /// </summary>
    private bool CanStart() => !IsRunning;
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
        if (!value)
        {
            RunStage = "Ожидание";
        }
    }

    partial void OnLogOnlyErrorsChanged(bool value)
    {
        RefreshFilteredLogs();
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
            ProgressText = $"Прогресс: {update.Current}/{update.Total} {update.Message}";
            if (!string.IsNullOrWhiteSpace(update.Message))
            {
                RunStage = update.Message;
            }
        });
        if (_runCts != null && _telegramPolicy != null)
        {
            _ = _telegramPolicy.NotifyProgressAsync(update, _runCts.Token);
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
    private void OpenSettings()
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
                await item.TestLibrary.RefreshCommand.ExecuteAsync(null);
            }
        }

        await RunsTab.RefreshCommand.ExecuteAsync(null);
        UpdateTelegramStatus();
        TelegramSettings.PropertyChanged += (_, _) => UpdateTelegramStatus();
    }

    private void UpdateTelegramStatus()
    {
        var isConfigured = TelegramSettings.Settings.Enabled &&
                           !string.IsNullOrWhiteSpace(TelegramSettings.Settings.BotToken) &&
                           !string.IsNullOrWhiteSpace(TelegramSettings.Settings.ChatId);
        TelegramStatus = isConfigured ? "Telegram: настроен" : "Telegram: не настроен";
        IsTelegramConfigured = isConfigured;
    }

    private void SetDatabaseStatus(string status, bool isOk)
    {
        DatabaseStatus = status;
        IsDatabaseOk = isOk;
    }

    private async Task SendTelegramAsync(string runId, Func<Task> action)
    {
        if (_telegramPolicy == null || !_telegramPolicy.IsEnabled)
        {
            return;
        }

        try
        {
            await action();
            await _runStore.AddTelegramNotificationAsync(new TelegramNotification
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                SentAt = DateTimeOffset.Now,
                Status = "Sent"
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logBus.Warn($"Telegram failed: {ex.Message}");
            await _runStore.AddTelegramNotificationAsync(new TelegramNotification
            {
                Id = Guid.NewGuid(),
                RunId = runId,
                SentAt = DateTimeOffset.Now,
                Status = "Failed",
                ErrorMessage = ex.Message
            }, CancellationToken.None);
        }
    }

    private ModuleItemViewModel? FindModuleItem(string moduleId)
    {
        return UiFamily.Modules.Concat(HttpFamily.Modules).Concat(NetFamily.Modules)
            .FirstOrDefault(item => item.Module.Id == moduleId);
    }

    private async Task RepeatRunAsync(string runId)
    {
        var detail = await _runStore.GetRunDetailAsync(runId, CancellationToken.None);
        if (detail == null)
        {
            return;
        }

        var moduleItem = FindModuleItem(detail.Run.ModuleType);
        if (moduleItem == null)
        {
            return;
        }

        var version = await _runStore.GetTestCaseVersionAsync(detail.Run.TestCaseId, detail.Run.TestCaseVersion, CancellationToken.None);
        if (version != null)
        {
            var settings = System.Text.Json.JsonSerializer.Deserialize(version.PayloadJson, moduleItem.Module.SettingsType);
            if (settings != null)
            {
                moduleItem.SettingsViewModel.UpdateFrom(settings);
            }
        }

        var profile = System.Text.Json.JsonSerializer.Deserialize<RunProfile>(detail.Run.ProfileSnapshotJson);
        if (profile != null)
        {
            RunProfile.SelectedProfile = profile;
        }

        SelectedTabIndex = moduleItem.Module.Family switch
        {
            TestFamily.UiTesting => 0,
            TestFamily.HttpTesting => 1,
            TestFamily.NetSec => 2,
            _ => 0
        };
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

        var testLibrary = new TestLibraryViewModel(_runStore, module, settingsVm);
        return new ModuleItemViewModel(module, settingsVm, testLibrary);
    }
}

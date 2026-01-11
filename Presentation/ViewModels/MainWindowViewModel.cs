using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly LogBus _logBus = new();
    private readonly ProgressBus _progressBus = new();
    private readonly ArtifactStore _artifactStore = new();
    private readonly Limits _limits = new();
    private readonly TestOrchestrator _orchestrator;

    private CancellationTokenSource? _runCts;
    private TelegramPolicy? _telegramPolicy;

    /// <summary>
    /// Инициализирует модули, вкладки и сервисы запуска.
    /// </summary>
    public MainWindowViewModel()
    {
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
        UiFamily = new ModuleFamilyViewModel("UI Testing", new ObservableCollection<ModuleItemViewModel>(
            Registry.GetByFamily(TestFamily.UiTesting).Select(CreateModuleItem)));
        HttpFamily = new ModuleFamilyViewModel("HTTP Testing", new ObservableCollection<ModuleItemViewModel>(
            Registry.GetByFamily(TestFamily.HttpTesting).Select(CreateModuleItem)));
        NetFamily = new ModuleFamilyViewModel("Network & Security", new ObservableCollection<ModuleItemViewModel>(
            Registry.GetByFamily(TestFamily.NetSec).Select(CreateModuleItem)));

        ReportsTab = new ReportsTabViewModel(_artifactStore.ReportsRoot);
        TelegramSettings = new TelegramSettingsViewModel(new TelegramSettings());

        _progressBus.ProgressChanged += OnProgressChanged;

        _orchestrator = new TestOrchestrator(new JsonReportWriter(_artifactStore), new HtmlReportWriter(_artifactStore));

        _ = Task.Run(ReadLogAsync);
    }

    public ModuleRegistry Registry { get; }
    public ModuleFamilyViewModel UiFamily { get; }
    public ModuleFamilyViewModel HttpFamily { get; }
    public ModuleFamilyViewModel NetFamily { get; }
    public ReportsTabViewModel ReportsTab { get; }
    public TelegramSettingsViewModel TelegramSettings { get; }

    public ObservableCollection<string> LogEntries { get; } = new();

    [ObservableProperty]
    private string statusText = "Status: Idle";

    [ObservableProperty]
    private string progressText = "Progress: 0/0";

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private int selectedTabIndex;

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
            _ => UiFamily.SelectedModule
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
        StatusText = $"Status: Running {moduleItem.DisplayName}";
        ProgressText = "Progress: 0/0";

        _runCts = new CancellationTokenSource();
        var notifier = CreateTelegramNotifier();
        _telegramPolicy = new TelegramPolicy(notifier, TelegramSettings.Settings);
        var ctx = new RunContext(_logBus, _progressBus, _artifactStore, _limits, notifier);

        if (_telegramPolicy.IsEnabled)
        {
            await _telegramPolicy.NotifyStartAsync(moduleItem.DisplayName, _runCts.Token);
        }

        try
        {
            await _orchestrator.RunAsync(moduleItem.Module, moduleItem.SettingsViewModel.Settings, ctx, _runCts.Token);
            if (_telegramPolicy.IsEnabled)
            {
                await _telegramPolicy.NotifyFinishAsync(moduleItem.DisplayName, TestStatus.Completed, _runCts.Token);
            }
        }
        catch (Exception ex)
        {
            await _telegramPolicy.NotifyErrorAsync(ex.Message, _runCts.Token);
        }
        finally
        {
            IsRunning = false;
            StatusText = "Status: Idle";
            ReportsTab.RefreshCommand.Execute(null);
            _telegramPolicy = null;
        }
    }

    /// <summary>
    /// Останавливает текущий запуск.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _runCts?.Cancel();
        StatusText = "Status: Stopping";
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
    }

    /// <summary>
    /// Считывает логи из шины и добавляет их в UI.
    /// </summary>
    private async Task ReadLogAsync()
    {
        await foreach (var line in _logBus.ReadAllAsync())
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => LogEntries.Add(line));
        }
    }

    /// <summary>
    /// Обновляет прогресс и отправляет уведомления о ходе выполнения.
    /// </summary>
    private void OnProgressChanged(ProgressUpdate update)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            ProgressText = $"Progress: {update.Current}/{update.Total} {update.Message}");
        if (_runCts != null && _telegramPolicy != null)
        {
            _ = _telegramPolicy.NotifyProgressAsync(update, _runCts.Token);
        }
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

    /// <summary>
    /// Создаёт ViewModel элемента модуля с соответствующими настройками.
    /// </summary>
    private static ModuleItemViewModel CreateModuleItem(ITestModule module)
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

        return new ModuleItemViewModel(module, settingsVm);
    }
}

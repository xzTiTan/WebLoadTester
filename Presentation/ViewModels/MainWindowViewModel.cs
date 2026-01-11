using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
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

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ModuleRegistry _registry;
    private readonly Limits _limits = new();
    private CancellationTokenSource? _cts;
    private Task? _currentRun;

    public MainWindowViewModel()
    {
        _registry = new ModuleRegistry();
        RegisterModules();

        UiModules = new ObservableCollection<ModuleViewModel>(_registry.GetByFamily(TestFamily.UiTesting)
            .Select(CreateModuleViewModel));
        HttpModules = new ObservableCollection<ModuleViewModel>(_registry.GetByFamily(TestFamily.HttpTesting)
            .Select(CreateModuleViewModel));
        NetModules = new ObservableCollection<ModuleViewModel>(_registry.GetByFamily(TestFamily.NetworkSecurity)
            .Select(CreateModuleViewModel));

        UiTab = new UiTestingTabViewModel(UiModules);
        HttpTab = new HttpTestingTabViewModel(HttpModules);
        NetTab = new NetSecTabViewModel(NetModules);

        var artifactStore = new ArtifactStore();
        ReportsTab = new ReportsTabViewModel(artifactStore.ReportsRoot);

        SelectedModule = UiModules.FirstOrDefault();

        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning && SelectedModule is not null);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        RestartCommand = new AsyncRelayCommand(RestartAsync, () => !IsRunning);
    }

    public UiTestingTabViewModel UiTab { get; }
    public HttpTestingTabViewModel HttpTab { get; }
    public NetSecTabViewModel NetTab { get; }
    public ReportsTabViewModel ReportsTab { get; }

    public ObservableCollection<ModuleViewModel> UiModules { get; }
    public ObservableCollection<ModuleViewModel> HttpModules { get; }
    public ObservableCollection<ModuleViewModel> NetModules { get; }

    public ObservableCollection<string> LogEntries { get; } = new();

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _progressText = "0%";

    [ObservableProperty]
    private ModuleViewModel? _selectedModule;

    [ObservableProperty]
    private bool _isRunning;

    public TelegramSettingsViewModel TelegramSettings { get; } = new();

    public IAsyncRelayCommand StartCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IAsyncRelayCommand RestartCommand { get; }

    private void RegisterModules()
    {
        _registry.Register(new UiScenarioModule());
        _registry.Register(new UiSnapshotModule());
        _registry.Register(new UiTimingModule());
        _registry.Register(new HttpFunctionalModule());
        _registry.Register(new HttpPerformanceModule());
        _registry.Register(new HttpAssetsModule());
        _registry.Register(new NetDiagnosticsModule());
        _registry.Register(new AvailabilityModule());
        _registry.Register(new SecurityBaselineModule());
        _registry.Register(new PreflightModule());
    }

    private ModuleViewModel CreateModuleViewModel(ITestModule module)
    {
        return module.Id switch
        {
            "ui-scenario" => new ModuleViewModel(module, new UiScenarioSettingsViewModel((UiScenarioSettings)module.CreateDefaultSettings())),
            "ui-snapshot" => new ModuleViewModel(module, new UiSnapshotSettingsViewModel((UiSnapshotSettings)module.CreateDefaultSettings())),
            "ui-timing" => new ModuleViewModel(module, new UiTimingSettingsViewModel((UiTimingSettings)module.CreateDefaultSettings())),
            "http-functional" => new ModuleViewModel(module, new HttpFunctionalSettingsViewModel((HttpFunctionalSettings)module.CreateDefaultSettings())),
            "http-performance" => new ModuleViewModel(module, new HttpPerformanceSettingsViewModel((HttpPerformanceSettings)module.CreateDefaultSettings())),
            "http-assets" => new ModuleViewModel(module, new HttpAssetsSettingsViewModel((HttpAssetsSettings)module.CreateDefaultSettings())),
            "net-diagnostics" => new ModuleViewModel(module, new NetDiagnosticsSettingsViewModel((NetDiagnosticsSettings)module.CreateDefaultSettings())),
            "availability" => new ModuleViewModel(module, new AvailabilitySettingsViewModel((AvailabilitySettings)module.CreateDefaultSettings())),
            "security-baseline" => new ModuleViewModel(module, new SecurityBaselineSettingsViewModel((SecurityBaselineSettings)module.CreateDefaultSettings())),
            "preflight" => new ModuleViewModel(module, new PreflightSettingsViewModel((PreflightSettings)module.CreateDefaultSettings())),
            _ => throw new InvalidOperationException($"Unknown module {module.Id}")
        };
    }

    private async Task StartAsync()
    {
        if (SelectedModule is null)
        {
            StatusText = "Select a module.";
            return;
        }

        IsRunning = true;
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
        LogEntries.Clear();
        ProgressText = "0%";
        StatusText = "Running...";

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var logBus = new LogBus();
        var progressBus = new ProgressBus();
        var artifactStore = new ArtifactStore();

        ITelegramNotifier? notifier = null;
        var settings = TelegramSettings.ToSettings();
        if (settings.Enabled && !string.IsNullOrWhiteSpace(settings.BotToken) && !string.IsNullOrWhiteSpace(settings.ChatId))
        {
            notifier = new TelegramNotifier(settings.BotToken, settings.ChatId);
        }

        var telegramPolicy = new TelegramPolicy(notifier, settings);
        var orchestrator = new TestOrchestrator(artifactStore, _limits, notifier);

        _ = Task.Run(() => ConsumeLogsAsync(logBus, ct), ct);
        _ = Task.Run(() => ConsumeProgressAsync(progressBus, ct), ct);

        if (telegramPolicy.IsEnabled)
        {
            await telegramPolicy.NotifyStartAsync($"Starting {SelectedModule.DisplayName}", ct).ConfigureAwait(false);
        }

        _currentRun = Task.Run(async () =>
        {
            var report = await orchestrator.RunAsync(SelectedModule.Module, SelectedModule.SettingsViewModel.Settings, logBus, progressBus, ct).ConfigureAwait(false);
            if (telegramPolicy.IsEnabled)
            {
                var message = report.Status == TestStatus.Error
                    ? $"{SelectedModule.DisplayName} failed"
                    : $"{SelectedModule.DisplayName} finished with {report.Status}";
                if (report.Status == TestStatus.Error)
                {
                    await telegramPolicy.NotifyErrorAsync(message, ct).ConfigureAwait(false);
                }
                await telegramPolicy.NotifyFinishAsync(message, ct).ConfigureAwait(false);
            }

            ReportsTab.LoadReports();
            return report;
        }, ct);

        try
        {
            await _currentRun.ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() => StatusText = "Completed");
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusText = "Stopped");
        }
        finally
        {
            IsRunning = false;
            StartCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
            RestartCommand.NotifyCanExecuteChanged();
        }
    }

    partial void OnSelectedModuleChanged(ModuleViewModel? value)
    {
        StartCommand.NotifyCanExecuteChanged();
    }

    private async Task ConsumeLogsAsync(LogBus bus, CancellationToken ct)
    {
        await foreach (var line in bus.ReadAllAsync(ct))
        {
            await Dispatcher.UIThread.InvokeAsync(() => LogEntries.Add(line));
        }
    }

    private async Task ConsumeProgressAsync(ProgressBus bus, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var update = await bus.ReadAsync(ct).ConfigureAwait(false);
            if (update is null)
            {
                continue;
            }
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ProgressText = $"{update.Current}/{update.Total} ({update.Percentage:F0}%)";
            });
        }
    }

    private void Stop()
    {
        _cts?.Cancel();
    }

    private async Task RestartAsync()
    {
        Stop();
        if (_currentRun is not null)
        {
            try
            {
                await _currentRun.ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
        await StartAsync().ConfigureAwait(false);
    }

    partial void OnIsRunningChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        RestartCommand.NotifyCanExecuteChanged();
    }
}

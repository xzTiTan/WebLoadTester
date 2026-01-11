using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Core.Services.Metrics;
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

namespace WebLoadTester.Presentation.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly ModuleRegistry _registry;
    private readonly JsonReportWriter _jsonWriter = new();
    private readonly HtmlReportWriter _htmlWriter = new();
    private readonly MetricsCalculator _metricsCalculator = new();

    private CancellationTokenSource? _cts;
    private Task? _logReaderTask;
    private LogBus? _logBus;
    private ProgressBus? _progressBus;

    public MainWindowViewModel()
    {
        _registry = new ModuleRegistry(new ITestModule[]
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
        });

        UiModules = new ObservableCollection<ModuleEntryViewModel>(_registry.ByFamily(TestFamily.UiTesting).Select(ModuleEntryViewModel.Create));
        HttpModules = new ObservableCollection<ModuleEntryViewModel>(_registry.ByFamily(TestFamily.HttpTesting).Select(ModuleEntryViewModel.Create));
        NetModules = new ObservableCollection<ModuleEntryViewModel>(_registry.ByFamily(TestFamily.NetSecurity).Select(ModuleEntryViewModel.Create));

        SelectedUiModule = UiModules.FirstOrDefault();
        SelectedHttpModule = HttpModules.FirstOrDefault();
        SelectedNetModule = NetModules.FirstOrDefault();

        Logs = new ObservableCollection<string>();
        TelegramSettings = new TelegramSettingsViewModel();
    }

    public ObservableCollection<ModuleEntryViewModel> UiModules { get; }
    public ObservableCollection<ModuleEntryViewModel> HttpModules { get; }
    public ObservableCollection<ModuleEntryViewModel> NetModules { get; }

    [ObservableProperty]
    private ModuleEntryViewModel? selectedUiModule;

    [ObservableProperty]
    private ModuleEntryViewModel? selectedHttpModule;

    [ObservableProperty]
    private ModuleEntryViewModel? selectedNetModule;

    [ObservableProperty]
    private string statusText = "Idle";

    [ObservableProperty]
    private string progressText = "0/0";

    public ObservableCollection<string> Logs { get; }

    public TelegramSettingsViewModel TelegramSettings { get; }

    [ObservableProperty]
    private int selectedTabIndex;

    [RelayCommand]
    private async Task StartAsync()
    {
        ModuleEntryViewModel? module = SelectedTabIndex switch
        {
            0 => SelectedUiModule,
            1 => SelectedHttpModule,
            2 => SelectedNetModule,
            _ => SelectedUiModule ?? SelectedHttpModule ?? SelectedNetModule
        };
        if (module is null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _logBus = new LogBus();
        _progressBus = new ProgressBus();
        _progressBus.Progressed += update => Dispatcher.UIThread.Post(() =>
            ProgressText = update.Total > 0 ? $"{update.Current}/{update.Total}" : update.Message);

        Logs.Clear();
        _logReaderTask = Task.Run(async () =>
        {
            await foreach (var line in _logBus.Reader.ReadAllAsync(_cts.Token))
            {
                Dispatcher.UIThread.Post(() => Logs.Add(line));
            }
        }, _cts.Token);

        var artifactStore = new ArtifactStore(_jsonWriter);
        ITelegramNotifier? notifier = null;
        if (TelegramSettings.Enabled && !string.IsNullOrWhiteSpace(TelegramSettings.BotToken) && !string.IsNullOrWhiteSpace(TelegramSettings.ChatId))
        {
            notifier = new TelegramNotifier(TelegramSettings.BotToken, TelegramSettings.ChatId);
        }

        var runContext = new RunContext(_logBus, _progressBus, artifactStore, Limits.Default, notifier);
        var policy = new TelegramPolicy(notifier, TelegramSettings.ToPolicySettings());
        var orchestrator = new TestOrchestrator(_jsonWriter, _htmlWriter, _metricsCalculator);

        StatusText = "Running";
        await policy.NotifyStartAsync(module.DisplayName, _cts.Token);
        TestReport report;
        try
        {
            report = await orchestrator.RunAsync(module.Module, module.Settings, runContext, _cts.Token);
            StatusText = report.Status.ToString();
        }
        catch (Exception ex)
        {
            StatusText = "Error";
            await policy.NotifyErrorAsync(module.DisplayName, ex.Message, _cts.Token);
            return;
        }

        if (report.Status == TestStatus.Error && report.ErrorMessage is not null)
        {
            await policy.NotifyErrorAsync(module.DisplayName, report.ErrorMessage, _cts.Token);
        }
        else
        {
            await policy.NotifyFinishAsync(report, _cts.Token);
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _cts?.Cancel();
        StatusText = "Stopping";
    }

    [RelayCommand]
    private void Restart()
    {
        Stop();
        StartCommand.Execute(null);
    }
}

public sealed partial class ModuleEntryViewModel : ObservableObject
{
    public ModuleEntryViewModel(ITestModule module, object settings)
    {
        Module = module;
        Settings = settings;
    }

    public ITestModule Module { get; }
    public object Settings { get; }
    public string DisplayName => Module.DisplayName;

    public static ModuleEntryViewModel Create(ITestModule module) => new(module, module.CreateDefaultSettings());
}

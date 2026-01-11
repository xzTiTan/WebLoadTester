using System.Collections.ObjectModel;
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

namespace WebLoadTester.Presentation.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ModuleRegistry _registry = new();
    private readonly TestOrchestrator _orchestrator = new();
    private readonly ArtifactStore _artifactStore = new();
    private readonly Limits _limits = new();
    private readonly LogBus _logBus = new();
    private readonly ProgressTracker _progress = new();

    private CancellationTokenSource? _runCts;

    public ObservableCollection<ModuleEntry> UiModules { get; } = new();
    public ObservableCollection<ModuleEntry> HttpModules { get; } = new();
    public ObservableCollection<ModuleEntry> NetModules { get; } = new();
    public ObservableCollection<string> LogEntries => _logBus.Entries;
    public TelegramSettingsViewModel TelegramSettings { get; } = new();

    [ObservableProperty]
    private int selectedTabIndex;

    [ObservableProperty]
    private ModuleEntry? selectedUiModule;

    [ObservableProperty]
    private ModuleEntry? selectedHttpModule;

    [ObservableProperty]
    private ModuleEntry? selectedNetModule;

    [ObservableProperty]
    private string statusText = "Status: Idle";

    [ObservableProperty]
    private string progressText = "Progress: 0/0";

    [ObservableProperty]
    private bool isRunning;

    public MainWindowViewModel()
    {
        RegisterModules();
        SelectedUiModule = UiModules.FirstOrDefault();
        SelectedHttpModule = HttpModules.FirstOrDefault();
        SelectedNetModule = NetModules.FirstOrDefault();

        _progress.ProgressChanged += (completed, total, message) =>
        {
            var totalText = total > 0 ? $"{completed}/{total}" : completed.ToString();
            ProgressText = $"Progress: {totalText} {message}".Trim();
        };
    }

    private int _progressCount;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        var moduleEntry = GetSelectedModule();
        if (moduleEntry == null)
        {
            _logBus.Log("Select a module first.");
            return;
        }

        var settings = moduleEntry.SettingsViewModel.BuildSettings();
        var validation = moduleEntry.Module.Validate(settings);
        if (validation.Count > 0)
        {
            foreach (var error in validation)
            {
                _logBus.Log($"Validation: {error}");
            }
            return;
        }

        IsRunning = true;
        StatusText = "Status: Running";
        ProgressText = "Progress: 0/0";
        _progressCount = 0;
        _runCts = new CancellationTokenSource();

        var notifier = CreateNotifier();
        var policy = new TelegramPolicy(notifier, TimeSpan.FromSeconds(Math.Max(1, TelegramSettings.RateLimitSeconds)));

        try
        {
            var context = new RunContext
            {
                Log = _logBus,
                Progress = new ProgressAdapter(count =>
                {
                    _progressCount = count.Completed;
                    _progress.Report(count.Completed, count.Total, count.Message);
                }),
                Artifacts = _artifactStore,
                Limits = _limits,
                Telegram = notifier
            };

            await policy.NotifyStartAsync(new TestReport
            {
                Meta = new ReportMeta { ModuleId = moduleEntry.Module.Id, ModuleName = moduleEntry.Module.DisplayName }
            }, _runCts.Token);

            var report = await _orchestrator.ExecuteAsync(moduleEntry.Module, settings, context, _runCts.Token);
            StatusText = $"Status: {report.Meta.Status}";
            ProgressText = $"Progress: {report.Results.Count}/{report.Results.Count}";
            _logBus.Log($"Report saved: {report.Artifacts.JsonPath}");
            await policy.NotifyFinishAsync(report, _runCts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Status: Stopped";
            _logBus.Log("Run stopped.");
        }
        catch (Exception ex)
        {
            StatusText = "Status: Error";
            _logBus.Log($"Error: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _runCts?.Cancel();
        _logBus.Log("Cancellation requested.");
    }

    [RelayCommand]
    private void ClearLog() => _logBus.Entries.Clear();

    public bool CanStart => !IsRunning;
    public bool CanStop => IsRunning;

    private ModuleEntry? GetSelectedModule()
    {
        return SelectedTabIndex switch
        {
            0 => SelectedUiModule,
            1 => SelectedHttpModule,
            2 => SelectedNetModule,
            _ => SelectedUiModule
        };
    }

    private ITelegramNotifier? CreateNotifier()
    {
        if (!TelegramSettings.Enabled || string.IsNullOrWhiteSpace(TelegramSettings.BotToken) || string.IsNullOrWhiteSpace(TelegramSettings.ChatId))
        {
            return null;
        }

        return new TelegramNotifier(TelegramSettings.BotToken, TelegramSettings.ChatId);
    }

    private void RegisterModules()
    {
        Register(new UiScenarioModule(), new UiScenarioSettingsViewModel(), UiModules);
        Register(new UiSnapshotModule(), new UiSnapshotSettingsViewModel(), UiModules);
        Register(new UiTimingModule(), new UiTimingSettingsViewModel(), UiModules);

        Register(new HttpFunctionalModule(), new HttpFunctionalSettingsViewModel(), HttpModules);
        Register(new HttpPerformanceModule(), new HttpPerformanceSettingsViewModel(), HttpModules);
        Register(new HttpAssetsModule(), new HttpAssetsSettingsViewModel(), HttpModules);

        Register(new NetDiagnosticsModule(), new NetDiagnosticsSettingsViewModel(), NetModules);
        Register(new AvailabilityModule(), new AvailabilitySettingsViewModel(), NetModules);
        Register(new SecurityBaselineModule(), new SecurityBaselineSettingsViewModel(), NetModules);
        Register(new PreflightModule(), new PreflightSettingsViewModel(), NetModules);
    }

    private void Register(ITestModule module, ISettingsViewModel settingsViewModel, ObservableCollection<ModuleEntry> target)
    {
        _registry.Register(module);
        target.Add(new ModuleEntry(module, settingsViewModel));
    }

    public override void Dispose()
    {
        base.Dispose();
        _logBus.Dispose();
        _runCts?.Dispose();
    }

    private sealed class ProgressAdapter : IProgressSink
    {
        private readonly Action<(int Completed, int Total, string? Message)> _callback;

        public ProgressAdapter(Action<(int Completed, int Total, string? Message)> callback)
        {
            _callback = callback;
        }

        public void Report(int completed, int total, string? message = null)
        {
            _callback((completed, total, message));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Domain;
using WebLoadTester.Services;

namespace WebLoadTester.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, ILogSink
{
    private readonly Channel<string> _logChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = true
    });

    private CancellationTokenSource? _logCts;
    private CancellationTokenSource? _runCts;

    public ObservableCollection<SelectorItem> CssSelectors { get; } = new();
    public ObservableCollection<string> LogEntries { get; } = new();

    public IReadOnlyList<TestType> TestTypes { get; } = Enum.GetValues<TestType>();
    public IReadOnlyList<StepErrorPolicy> StepErrorPolicies { get; } = Enum.GetValues<StepErrorPolicy>();

    [ObservableProperty]
    private TestType selectedTestType = TestType.E2E;

    [ObservableProperty]
    private string targetUrl = "https://www.google.com";

    [ObservableProperty]
    private int totalRuns = 10;

    [ObservableProperty]
    private int concurrency = 1;

    [ObservableProperty]
    private int timeoutSeconds = 20;

    [ObservableProperty]
    private bool headless = true;

    [ObservableProperty]
    private bool screenshotAfterRun = false;

    [ObservableProperty]
    private StepErrorPolicy errorPolicy = StepErrorPolicy.SkipStep;

    [ObservableProperty]
    private int durationMinutes = 10;

    [ObservableProperty]
    private int rampStep = 5;

    [ObservableProperty]
    private int rampDelaySeconds = 10;

    [ObservableProperty]
    private int runsPerLevel = 10;

    [ObservableProperty]
    private string statusText = "Статус: Ожидание";

    [ObservableProperty]
    private string progressText = "Прогресс: 0/0";

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private SelectorItem? selectedSelector;

    [ObservableProperty]
    private bool telegramEnabled;

    [ObservableProperty]
    private bool telegramSendScreens;

    [ObservableProperty]
    private int telegramMode;

    [ObservableProperty]
    private string telegramToken = string.Empty;

    [ObservableProperty]
    private string telegramChatId = string.Empty;

    public bool IsStress => SelectedTestType == TestType.Stress;
    public bool IsEndurance => SelectedTestType == TestType.Endurance;
    public bool IsStressMode => SelectedTestType == TestType.Stress;
    public bool IsEnduranceMode => SelectedTestType == TestType.Endurance;

    public bool CanStart => !IsRunning;
    public bool CanStop => IsRunning;
    public bool HasSelectedSelector => SelectedSelector is not null;
    public bool CanMoveUp
    {
        get
        {
            if (SelectedSelector is null) return false;
            var index = CssSelectors.IndexOf(SelectedSelector);
            return index > 0;
        }
    }

    public bool CanMoveDown
    {
        get
        {
            if (SelectedSelector is null) return false;
            var index = CssSelectors.IndexOf(SelectedSelector);
            return index >= 0 && index < CssSelectors.Count - 1;
        }
    }

    public MainWindowViewModel()
    {
        CssSelectors.Add(new SelectorItem { Text = "input[name='q']" });
        CssSelectors.Add(new SelectorItem { Text = "input[type='submit']" });
        SelectedSelector = CssSelectors.FirstOrDefault();

        _logCts = new CancellationTokenSource();
        _ = Task.Run(() => ConsumeLogAsync(_logCts.Token));

        CssSelectors.CollectionChanged += (_, _) => UpdateSelectorCommandStates();
    }

    partial void OnSelectedTestTypeChanged(TestType value)
    {
        OnPropertyChanged(nameof(IsStress));
        OnPropertyChanged(nameof(IsEndurance));
        OnPropertyChanged(nameof(IsStressMode));
        OnPropertyChanged(nameof(IsEnduranceMode));
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        StartCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedSelectorChanged(SelectorItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedSelector));
        OnPropertyChanged(nameof(CanMoveUp));
        OnPropertyChanged(nameof(CanMoveDown));
        UpdateSelectorCommandStates();
    }

    public event Action<SelectorItem>? RequestFocusSelector;

    [RelayCommand]
    private void AddSelector()
    {
        var item = new SelectorItem { Text = string.Empty };
        CssSelectors.Add(item);
        SelectedSelector = item;
        RequestFocusSelector?.Invoke(item);
        UpdateSelectorCommandStates();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSelector))]
    private void RemoveSelector(SelectorItem? selector)
    {
        var target = selector ?? SelectedSelector;
        if (target == null)
        {
            return;
        }

        var index = CssSelectors.IndexOf(target);
        CssSelectors.Remove(target);

        if (CssSelectors.Count == 0)
        {
            SelectedSelector = null;
        }
        else
        {
            var nextIndex = Math.Min(index, CssSelectors.Count - 1);
            SelectedSelector = CssSelectors[nextIndex];
        }

        UpdateSelectorCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedSelector == null) return;
        var index = CssSelectors.IndexOf(SelectedSelector);
        if (index <= 0) return;
        CssSelectors.Move(index, index - 1);
        UpdateSelectorCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedSelector == null) return;
        var index = CssSelectors.IndexOf(SelectedSelector);
        if (index < 0 || index >= CssSelectors.Count - 1) return;
        CssSelectors.Move(index, index + 1);
        UpdateSelectorCommandStates();
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task Start()
    {
        if (IsRunning)
        {
            Log("Уже запущено");
            return;
        }

        if (string.IsNullOrWhiteSpace(TargetUrl))
        {
            Log("URL пустой");
            return;
        }

        var scenarioSteps = CssSelectors.Select(s => s.Text)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (scenarioSteps.Count == 0)
        {
            Log("Шаги отсутствуют, будет только открытие страницы (допустимо)");
        }

        IsRunning = true;
        StatusText = "Статус: Выполняется";
        ProgressText = "Прогресс: 0/0";
        _runCts = new CancellationTokenSource();

        try
        {
            var settings = new RunSettings
            {
                TargetUrl = TargetUrl,
                TotalRuns = Math.Max(1, TotalRuns),
                Concurrency = Math.Clamp(Concurrency, 1, 50),
                TimeoutSeconds = Math.Max(1, TimeoutSeconds),
                Headless = Headless,
                ScreenshotAfterRun = ScreenshotAfterRun,
                StepErrorPolicy = ErrorPolicy,
                TestType = SelectedTestType,
                StressStep = Math.Max(1, RampStep),
                StressPauseSeconds = Math.Max(0, RampDelaySeconds),
                RunsPerLevel = Math.Max(1, RunsPerLevel),
                EnduranceMinutes = Math.Max(1, DurationMinutes),
                Telegram = new TelegramSettings
                {
                    Enabled = TelegramEnabled,
                    SendScreenshots = TelegramSendScreens,
                    Mode = TelegramMode,
                    Token = TelegramToken,
                    ChatId = TelegramChatId
                }
            };

            var scenario = new Scenario
            {
                Steps = scenarioSteps.Select(s => new ScenarioStep { Selector = s }).ToList()
            };

            if (settings.ScreenshotAfterRun)
            {
                settings.ScreenshotDirectory = Path.Combine("screenshots", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Directory.CreateDirectory(settings.ScreenshotDirectory);
            }

            var planBuilder = new TestPlanBuilder();
            var plan = planBuilder.Build(settings);
            foreach (var phase in plan.Phases)
            {
                Log($"План: {phase.Name} | conc={phase.Concurrency} | runs={(phase.Runs?.ToString() ?? "∞")} | duration={(phase.Duration?.ToString() ?? "—")} | pause={phase.PauseAfterSeconds}s");
            }

            await using var runner = new PlaywrightWebUiRunner();
            var orchestrator = new TestOrchestrator();

            var context = new RunContext
            {
                Logger = this,
                Runner = runner,
                Scenario = scenario,
                Settings = settings,
                Progress = UpdateProgress,
                Cancellation = _runCts
            };

            Log($"Старт [{SelectedTestType}] url={TargetUrl}, runs={TotalRuns}, conc={Concurrency}");
            var result = await orchestrator.ExecuteAsync(context, plan, _runCts.Token);
            var ok = result.Runs.Count(r => r.Success);
            var fail = result.Runs.Count - ok;
            StatusText = "Статус: Готово";
            ProgressText = $"Прогресс: {result.Runs.Count}/{result.Runs.Count}  ОК: {ok}  Ошибок: {fail}";
            Log($"Готово. ОК={ok} Ошибок={fail} Отчёт: {result.ReportPath}");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Статус: Остановлено";
            Log("Запуск остановлен");
        }
        catch (Exception ex)
        {
            StatusText = "Статус: Ошибка";
            Log($"Ошибка: {ex.Message}");
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
        Log("Остановлено пользователем");
    }

    public void Log(string message)
    {
        AppendLog(message);
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logChannel.Writer.TryWrite(line);
    }

    private async Task ConsumeLogAsync(CancellationToken ct)
    {
        try
        {
            while (await _logChannel.Reader.WaitToReadAsync(ct))
            {
                while (_logChannel.Reader.TryRead(out var line))
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        LogEntries.Add(line);
                        if (LogEntries.Count > 3000)
                        {
                            LogEntries.RemoveAt(0);
                        }
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void UpdateProgress(int done, int total)
    {
        var totalText = total > 0 ? $"{done}/{total}" : done.ToString();
        ProgressText = $"Прогресс: {totalText}";
    }

    private void UpdateSelectorCommandStates()
    {
        RemoveSelectorCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    public override void Dispose()
    {
        base.Dispose();
        _logCts?.Cancel();
        _runCts?.Cancel();
        _logCts?.Dispose();
        _runCts?.Dispose();
    }
}

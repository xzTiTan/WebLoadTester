using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Infrastructure.Storage;
using WebLoadTester.Presentation.ViewModels.Controls;
using WebLoadTester.Presentation.ViewModels.Workspace;

namespace WebLoadTester.Presentation.ViewModels.Shell;

public partial class AppShellViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _backend;
    private readonly UiLayoutState _layoutState;
    private readonly DispatcherTimer _layoutSaveDebounce;
    private long _backendLogReceivedCount;
    private long _logDrawerAppendedCount;

    public AppShellViewModel()
        : this(new MainWindowViewModel())
    {
    }

    public AppShellViewModel(MainWindowViewModel backend)
    {
        _backend = backend;
        _layoutState = _backend.GetUiLayoutStateSnapshot();

        _layoutSaveDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _layoutSaveDebounce.Tick += async (_, _) =>
        {
            _layoutSaveDebounce.Stop();
            await PersistLayoutStateAsync();
        };

        LogDrawer = new LogDrawerViewModel(_layoutState, ScheduleLayoutSave);

        UiModules = new ModuleFamilyViewModel("UI тестирование", _backend, _backend.UiFamily, LogDrawer, _layoutState, ScheduleLayoutSave);
        HttpModules = new ModuleFamilyViewModel("HTTP тестирование", _backend, _backend.HttpFamily, LogDrawer, _layoutState, ScheduleLayoutSave);
        NetSecModules = new ModuleFamilyViewModel("Сеть и безопасность", _backend, _backend.NetFamily, LogDrawer, _layoutState, ScheduleLayoutSave);

        IsRunning = _backend.IsRunning;

        _backend.RunsTab.ConfigureRepeatRun(RepeatRunFromReportAsync);
        _backend.RunsTab.SetRunningStateProvider(() => IsRunning);

        OpenSettingsCommand = _backend.OpenSettingsCommand;
        OpenRunsFolderCommand = _backend.OpenRunsFolderCommand;
        ToggleLogDrawerCommand = new RelayCommand(() => LogDrawer.IsExpanded = !LogDrawer.IsExpanded);
        StartHotkeyCommand = new AsyncRelayCommand(StartFromHotkeyAsync);
        StopHotkeyCommand = new RelayCommand(StopFromHotkey);

        SyncExistingLogs();
        _backend.LogEntries.CollectionChanged += OnBackendLogEntriesChanged;
        _backend.PropertyChanged += OnBackendPropertyChanged;
        _backend.RepeatRunPrepared += OnRepeatRunPrepared;
    }

    public ModuleFamilyViewModel UiModules { get; }
    public ModuleFamilyViewModel HttpModules { get; }
    public ModuleFamilyViewModel NetSecModules { get; }

    public int SelectedTabIndex
    {
        get => _backend.SelectedTabIndex;
        set
        {
            if (_backend.SelectedTabIndex == value)
            {
                return;
            }

            _backend.SelectedTabIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentTabContent));
        }
    }

    public object CurrentTabContent => SelectedTabIndex switch
    {
        0 => UiModules,
        1 => HttpModules,
        2 => NetSecModules,
        _ => _backend.RunsTab
    };

    [ObservableProperty]
    private bool isRunning;

    public bool IsIdle => !IsRunning;
    public string StatusText => IsRunning ? "Выполняется" : "Ожидание";

    public LogDrawerViewModel LogDrawer { get; }

    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand OpenRunsFolderCommand { get; }
    public IRelayCommand ToggleLogDrawerCommand { get; }
    public IAsyncRelayCommand StartHotkeyCommand { get; }
    public IRelayCommand StopHotkeyCommand { get; }

    private void OnBackendPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsRunning))
        {
            IsRunning = _backend.IsRunning;
            _backend.RunsTab.RefreshRunningState();
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.SelectedTabIndex))
        {
            OnPropertyChanged(nameof(SelectedTabIndex));
            OnPropertyChanged(nameof(CurrentTabContent));
        }
    }

    private async Task StartFromHotkeyAsync()
    {
        var runControl = GetSelectedRunControl();
        if (runControl != null)
        {
            await runControl.StartCommand.ExecuteAsync(null);
            return;
        }

        if (_backend.StartCommand.CanExecute(null))
        {
            await _backend.StartCommand.ExecuteAsync(null);
        }
    }

    private void StopFromHotkey()
    {
        var runControl = GetSelectedRunControl();
        if (runControl != null && runControl.CanStop)
        {
            runControl.StopCommand.Execute(null);
            return;
        }

        if (_backend.StopCommand.CanExecute(null))
        {
            _backend.StopCommand.Execute(null);
        }
    }

    private RunControlViewModel? GetSelectedRunControl()
    {
        return SelectedTabIndex switch
        {
            0 => UiModules.Workspace.RunControl,
            1 => HttpModules.Workspace.RunControl,
            2 => NetSecModules.Workspace.RunControl,
            _ => null
        };
    }

    public async Task RepeatRunFromReportAsync(string runId)
    {
        await _backend.RepeatRunFromReportAsync(runId);
    }

    private void OnRepeatRunPrepared()
    {
        var family = SelectedTabIndex switch
        {
            0 => UiModules,
            1 => HttpModules,
            2 => NetSecModules,
            _ => null
        };

        if (family == null)
        {
            return;
        }

        family.Workspace.RefreshWorkspaceValidationErrors();
        family.Workspace.RequestScrollToTop();
        family.Workspace.RequestRunControlFocus();
    }

    private void OnBackendLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<string>())
            {
                _backendLogReceivedCount++;
                LogDrawer.Append(ToLogLine(item));
                _logDrawerAppendedCount++;
            }

            if (_logDrawerAppendedCount > 0 && _logDrawerAppendedCount % 200 == 0)
            {
                Debug.WriteLine($"[LogPipeline] backend={_backendLogReceivedCount}, drawer={_logDrawerAppendedCount}");
            }

            return;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            LogDrawer.ResetLines();
            _backendLogReceivedCount = 0;
            _logDrawerAppendedCount = 0;
            SyncExistingLogs();
            Debug.WriteLine($"[LogPipeline] reset handled, backend={_backendLogReceivedCount}, drawer={_logDrawerAppendedCount}");
            return;
        }

        if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Move)
        {
            LogDrawer.ResetLines();
            _backendLogReceivedCount = 0;
            _logDrawerAppendedCount = 0;
            SyncExistingLogs();
        }
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(StatusText));
    }

    private void SyncExistingLogs()
    {
        foreach (var raw in _backend.LogEntries)
        {
            _backendLogReceivedCount++;
            LogDrawer.Append(ToLogLine(raw));
            _logDrawerAppendedCount++;
        }

        if (_logDrawerAppendedCount > 0)
        {
            Debug.WriteLine($"[LogPipeline] sync backend={_backendLogReceivedCount}, drawer={_logDrawerAppendedCount}");
        }
    }

    private void ScheduleLayoutSave()
    {
        _layoutSaveDebounce.Stop();
        _layoutSaveDebounce.Start();
    }

    private async Task PersistLayoutStateAsync()
    {
        var activeWorkspace = SelectedTabIndex switch
        {
            0 => UiModules.Workspace,
            1 => HttpModules.Workspace,
            2 => NetSecModules.Workspace,
            _ => UiModules.Workspace
        };

        _layoutState.LeftNavWidth = activeWorkspace.LeftNavWidth;
        _layoutState.DetailsWidth = activeWorkspace.DetailsWidth;
        _layoutState.IsDetailsVisible = activeWorkspace.IsDetailsVisible;
        _layoutState.IsLogExpanded = LogDrawer.IsExpanded;
        _layoutState.IsLogOnlyErrors = LogDrawer.OnlyErrors;
        _layoutState.LogFilterText = LogDrawer.FilterText;

        await _backend.SaveUiLayoutStateAsync(_layoutState);
    }

    private static LogLineViewModel ToLogLine(string raw)
    {
        var level = raw.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
            ? "ERROR"
            : raw.Contains("WARN", StringComparison.OrdinalIgnoreCase)
                ? "WARN"
                : "INFO";

        var moduleId = "core";
        var start = raw.IndexOf('[');
        var end = start >= 0 ? raw.IndexOf(']', start + 1) : -1;
        if (start >= 0 && end > start + 1)
        {
            moduleId = raw.Substring(start + 1, end - start - 1);
        }

        return new LogLineViewModel(DateTimeOffset.Now, level, moduleId, raw);
    }
}

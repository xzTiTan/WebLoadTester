using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
    private int _processedBackendLogCount;

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

        Tabs = new ObservableCollection<TabViewModel>
        {
            new("UI", "UI тестирование", UiModules),
            new("HTTP", "HTTP тестирование", HttpModules),
            new("Сеть", "Сеть и безопасность", NetSecModules),
            new("Прогоны", "Прогоны", _backend.RunsTab)
        };

        selectedTab = Tabs[Math.Clamp(_backend.SelectedTabIndex, 0, Tabs.Count - 1)];
        IsRunning = _backend.IsRunning;

        _backend.RunsTab.ConfigureRepeatRun(RepeatRunFromReportAsync);
        _backend.RunsTab.SetRunningStateProvider(() => IsRunning);

        OpenSettingsCommand = _backend.OpenSettingsCommand;
        OpenRunsFolderCommand = _backend.OpenRunsFolderCommand;
        ToggleLogDrawerCommand = new RelayCommand(() => LogDrawer.IsExpanded = !LogDrawer.IsExpanded);
        StartHotkeyCommand = new AsyncRelayCommand(StartFromHotkeyAsync);
        StopHotkeyCommand = new RelayCommand(StopFromHotkey);

        AppendPendingLogs();
        _backend.LogEntries.CollectionChanged += OnBackendLogEntriesChanged;
        _backend.PropertyChanged += OnBackendPropertyChanged;
        _backend.RepeatRunPrepared += OnRepeatRunPrepared;
    }

    public ObservableCollection<TabViewModel> Tabs { get; }
    public ModuleFamilyViewModel UiModules { get; }
    public ModuleFamilyViewModel HttpModules { get; }
    public ModuleFamilyViewModel NetSecModules { get; }

    [ObservableProperty]
    private TabViewModel selectedTab;

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

    partial void OnSelectedTabChanged(TabViewModel value)
    {
        var index = Tabs.IndexOf(value);
        if (index >= 0 && _backend.SelectedTabIndex != index)
        {
            _backend.SelectedTabIndex = index;
        }

    }

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
            var index = Math.Clamp(_backend.SelectedTabIndex, 0, Tabs.Count - 1);
            if (!ReferenceEquals(SelectedTab, Tabs[index]))
            {
                SelectedTab = Tabs[index];
            }
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
        return SelectedTab.ContentVm switch
        {
            ModuleFamilyViewModel family => family.Workspace.RunControl,
            _ => null
        };
    }

    public async Task RepeatRunFromReportAsync(string runId)
    {
        await _backend.RepeatRunFromReportAsync(runId);
    }

    private void OnRepeatRunPrepared()
    {
        if (SelectedTab.ContentVm is not ModuleFamilyViewModel family)
        {
            return;
        }

        family.Workspace.RefreshWorkspaceValidationErrors();
        family.Workspace.RequestScrollToTop();
        family.Workspace.RequestRunControlFocus();
    }

    private void OnBackendLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AppendPendingLogs();
    }


    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(StatusText));
    }

    private void AppendPendingLogs()
    {
        while (_processedBackendLogCount < _backend.LogEntries.Count)
        {
            var raw = _backend.LogEntries[_processedBackendLogCount++];
            LogDrawer.Append(ToLogLine(raw));
        }
    }

    private void ScheduleLayoutSave()
    {
        _layoutSaveDebounce.Stop();
        _layoutSaveDebounce.Start();
    }

    private async Task PersistLayoutStateAsync()
    {
        var activeWorkspace = SelectedTab.ContentVm is ModuleFamilyViewModel family ? family.Workspace : UiModules.Workspace;

        _layoutState.LeftNavWidth = activeWorkspace.LeftNavWidth;
        _layoutState.DetailsWidth = activeWorkspace.DetailsWidth;
        _layoutState.IsDetailsVisible = activeWorkspace.IsDetailsVisible;
        _layoutState.IsTestCaseExpanded = activeWorkspace.IsTestCaseExpanded;
        _layoutState.IsRunProfileExpanded = activeWorkspace.IsRunProfileExpanded;
        _layoutState.IsModuleSettingsExpanded = activeWorkspace.IsModuleSettingsExpanded;
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

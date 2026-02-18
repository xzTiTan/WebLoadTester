using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Presentation.ViewModels.Controls;
using WebLoadTester.Presentation.ViewModels.Workspace;

namespace WebLoadTester.Presentation.ViewModels.Shell;

public partial class AppShellViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _backend;
    private int _processedBackendLogCount;

    public AppShellViewModel()
        : this(new MainWindowViewModel())
    {
    }

    public AppShellViewModel(MainWindowViewModel backend)
    {
        _backend = backend;
        LogDrawer = new LogDrawerViewModel();

        UiModules = new ModuleFamilyViewModel("UI тестирование", _backend, _backend.UiFamily);
        HttpModules = new ModuleFamilyViewModel("HTTP тестирование", _backend, _backend.HttpFamily);
        NetSecModules = new ModuleFamilyViewModel("Сеть и безопасность", _backend, _backend.NetFamily);

        Tabs = new ObservableCollection<TabViewModel>
        {
            new("UI тестирование", UiModules),
            new("HTTP тестирование", HttpModules),
            new("Сеть и безопасность", NetSecModules),
            new("Прогоны", _backend.RunsTab)
        };

        selectedTab = Tabs[Math.Clamp(_backend.SelectedTabIndex, 0, Tabs.Count - 1)];
        IsRunning = _backend.IsRunning;

        OpenSettingsCommand = _backend.OpenSettingsCommand;
        OpenRunsFolderCommand = _backend.OpenRunsFolderCommand;

        AppendPendingLogs();
        _backend.LogEntries.CollectionChanged += OnBackendLogEntriesChanged;
        _backend.PropertyChanged += OnBackendPropertyChanged;
    }

    public ObservableCollection<TabViewModel> Tabs { get; }
    public ModuleFamilyViewModel UiModules { get; }
    public ModuleFamilyViewModel HttpModules { get; }
    public ModuleFamilyViewModel NetSecModules { get; }

    [ObservableProperty]
    private TabViewModel selectedTab;

    [ObservableProperty]
    private bool isRunning;

    public LogDrawerViewModel LogDrawer { get; }

    public IRelayCommand OpenSettingsCommand { get; }
    public IRelayCommand OpenRunsFolderCommand { get; }

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

    private void OnBackendLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AppendPendingLogs();
    }

    private void AppendPendingLogs()
    {
        while (_processedBackendLogCount < _backend.LogEntries.Count)
        {
            var raw = _backend.LogEntries[_processedBackendLogCount++];
            LogDrawer.Append(ToLogLine(raw));
        }
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

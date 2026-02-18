using System;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class DetailsPaneViewModel : ObservableObject
{
    private readonly MainWindowViewModel _backend;
    private readonly LogDrawerViewModel _logDrawer;
    private readonly ModuleWorkspaceViewModel _workspace;

    public DetailsPaneViewModel(MainWindowViewModel backend, LogDrawerViewModel logDrawer, ModuleWorkspaceViewModel workspace)
    {
        _backend = backend;
        _logDrawer = logDrawer;
        _workspace = workspace;

        _backend.PropertyChanged += OnStateChanged;
        _workspace.PropertyChanged += OnStateChanged;
        _logDrawer.VisibleLines.CollectionChanged += OnVisibleLinesChanged;
    }

    public string IdentityLine => $"{_workspace.ModuleDisplayName} ({_workspace.ModuleId})";
    public string StateLine => _backend.StatusText;
    public string MetricLine1 => _backend.ProgressText;
    public string MetricLine2 => _backend.RunStage;
    public string LastErrorShort => _logDrawer.GetLastErrorShort(_workspace.ModuleId);
    public bool HasError => !string.IsNullOrWhiteSpace(LastErrorShort);

    public string RunFolderPath => _backend.SelectedModule?.LastReport != null
        ? $"runs/{_backend.SelectedModule.LastReport.RunId}"
        : string.Empty;

    public bool HasRunFolder => _backend.SelectedModule?.LastReport != null;
    public bool HasHtml => !string.IsNullOrWhiteSpace(_backend.SelectedModule?.LastReport?.Artifacts.HtmlPath);
    public bool HasJson => !string.IsNullOrWhiteSpace(_backend.SelectedModule?.LastReport?.Artifacts.JsonPath);

    public bool TelegramEnabled => _backend.TelegramSettings.Settings.Enabled;
    public bool NotifyOnError
    {
        get => _backend.TelegramSettings.Settings.NotifyOnError;
        set => _backend.TelegramSettings.Settings.NotifyOnError = value;
    }

    public bool NotifyOnFinish
    {
        get => _backend.TelegramSettings.Settings.NotifyOnFinish;
        set => _backend.TelegramSettings.Settings.NotifyOnFinish = value;
    }

    public bool CanEditTelegram => !_backend.IsRunning;

    public IRelayCommand OpenFolderCommand => _backend.OpenLatestRunFolderCommand;
    public IRelayCommand OpenHtmlCommand => _backend.OpenLatestHtmlCommand;
    public IRelayCommand OpenJsonCommand => _backend.OpenLatestJsonCommand;

    [RelayCommand]
    private void ShowInLog()
    {
        _logDrawer.ShowInLog(_workspace.ModuleId);
    }

    private void OnVisibleLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(LastErrorShort));
        OnPropertyChanged(nameof(HasError));
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IdentityLine));
        OnPropertyChanged(nameof(StateLine));
        OnPropertyChanged(nameof(MetricLine1));
        OnPropertyChanged(nameof(MetricLine2));
        OnPropertyChanged(nameof(LastErrorShort));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(RunFolderPath));
        OnPropertyChanged(nameof(HasRunFolder));
        OnPropertyChanged(nameof(HasHtml));
        OnPropertyChanged(nameof(HasJson));
        OnPropertyChanged(nameof(TelegramEnabled));
        OnPropertyChanged(nameof(NotifyOnError));
        OnPropertyChanged(nameof(NotifyOnFinish));
        OnPropertyChanged(nameof(CanEditTelegram));
    }
}

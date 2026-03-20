using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class RunControlViewModel : ObservableObject
{
    private readonly MainWindowViewModel _backend;
    private readonly ModuleWorkspaceViewModel _workspace;

    public RunControlViewModel(MainWindowViewModel backend, ModuleWorkspaceViewModel workspace)
    {
        _backend = backend;
        _workspace = workspace;

        StartCommand = new AsyncRelayCommand(StartAsync, CanStartRun);
        InstallChromiumCommand = new AsyncRelayCommand(InstallChromiumAsync, CanInstallChromiumNow);

        _backend.PropertyChanged += OnBackendPropertyChanged;
        _workspace.PropertyChanged += OnWorkspacePropertyChanged;
        _workspace.WorkspaceValidationErrors.CollectionChanged += (_, _) => RaiseState();
    }

    public IAsyncRelayCommand StartCommand { get; }
    public IRelayCommand StopCommand => _backend.StopCommand;
    public IRelayCommand OpenRunFolderCommand => _backend.OpenLatestRunFolderCommand;
    public IRelayCommand OpenHtmlReportCommand => _backend.OpenLatestHtmlCommand;
    public IRelayCommand OpenJsonReportCommand => _backend.OpenLatestJsonCommand;
    public IRelayCommand OpenRunsTabCommand => _backend.OpenRunsTabCommand;
    public IAsyncRelayCommand InstallChromiumCommand { get; }

    public string StatusText => _backend.StatusText;
    public string RunStateLabel => BuildRunStateLabel();
    public string ProgressDetails => _backend.ProgressText;
    public string StageDetails => $"Этап: {_backend.RunStage}";
    public string RunIdLine => _backend.IsRunning && !string.IsNullOrWhiteSpace(_backend.CurrentRunId) && _backend.CurrentRunId != "—"
        ? $"RunId: {_backend.CurrentRunId}"
        : _backend.SelectedModule?.LastReport is { } report
            ? $"RunId: {report.RunId}"
            : "RunId: —";
    public string FinishedAtLine => _backend.SelectedModule?.LastReport is { } report
        ? $"Завершён: {report.FinishedAt:dd.MM.yyyy HH:mm:ss}"
        : "Завершён: —";
    public string LastRunInfo => _backend.SelectedModule?.LastReport is { } report
        ? $"Последний прогон: {report.FinishedAt:dd.MM.yyyy HH:mm:ss}"
        : "Последний прогон: —";
    public double ProgressValue => _backend.ProgressPercent;
    public bool IsIndeterminate => _backend.IsProgressIndeterminate;
    public bool CanStart => StartCommand.CanExecute(null);
    public bool CanStop => _backend.StopCommand.CanExecute(null);

    public bool HasRunFolder => _backend.SelectedModule?.LastReport != null;
    public bool HasHtmlReport => !string.IsNullOrWhiteSpace(_backend.SelectedModule?.LastReport?.Artifacts.HtmlPath);
    public bool HasJsonReport => !string.IsNullOrWhiteSpace(_backend.SelectedModule?.LastReport?.Artifacts.JsonPath);

    public ObservableCollection<string> ValidationErrors => _workspace.WorkspaceValidationErrors;
    public bool HasValidationErrors => ValidationErrors.Count > 0;
    public bool HasChromiumValidationError => ValidationErrors.Any(x => x.Contains("Chromium", StringComparison.OrdinalIgnoreCase));
    public bool CanInstallChromium => CanInstallChromiumNow();

    [ObservableProperty]
    private int focusRequestToken;

    public void RequestStartFocus()
    {
        FocusRequestToken++;
    }


    private async Task InstallChromiumAsync()
    {
        if (!CanInstallChromiumNow())
        {
            return;
        }

        await _backend.InstallPlaywrightBrowsersCommand.ExecuteAsync(null);
        _workspace.RefreshWorkspaceValidationErrors();
    }

    private bool CanInstallChromiumNow()
    {
        return HasChromiumValidationError
               && _backend.InstallPlaywrightBrowsersCommand.CanExecute(null)
               && _backend.CanInstallPlaywright;
    }
    private async Task StartAsync()
    {
        if (!CanStartRun())
        {
            return;
        }

        await _backend.StartCommand.ExecuteAsync(null);
    }

    private bool CanStartRun()
    {
        return _workspace.IsIdle
               && _backend.StartCommand.CanExecute(null);
    }

    private void OnBackendPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaiseState();
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModuleWorkspaceViewModel.IsIdle)
            or nameof(ModuleWorkspaceViewModel.WorkspaceValidationErrors)
            or nameof(ModuleWorkspaceViewModel.HasWorkspaceValidationErrors))
        {
            RaiseState();
        }
    }

    private string BuildRunStateLabel()
    {
        if (_backend.IsRunning)
        {
            return "Выполняется";
        }

        if (_backend.StatusText.Contains("успешно", StringComparison.OrdinalIgnoreCase)) return "Завершён успешно";
        if (_backend.StatusText.Contains("ошиб", StringComparison.OrdinalIgnoreCase)) return "Завершён с ошибками";
        if (_backend.StatusText.Contains("останов", StringComparison.OrdinalIgnoreCase)) return "Остановлен";
        if (_backend.StatusText.Contains("отмен", StringComparison.OrdinalIgnoreCase)) return "Остановлен";
        return "Готов к запуску";
    }

    private void RaiseState()
    {
        StartCommand.NotifyCanExecuteChanged();
        InstallChromiumCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RunStateLabel));
        OnPropertyChanged(nameof(ProgressDetails));
        OnPropertyChanged(nameof(StageDetails));
        OnPropertyChanged(nameof(RunIdLine));
        OnPropertyChanged(nameof(FinishedAtLine));
        OnPropertyChanged(nameof(LastRunInfo));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(HasRunFolder));
        OnPropertyChanged(nameof(HasHtmlReport));
        OnPropertyChanged(nameof(HasJsonReport));
        OnPropertyChanged(nameof(ValidationErrors));
        OnPropertyChanged(nameof(HasValidationErrors));
        OnPropertyChanged(nameof(HasChromiumValidationError));
        OnPropertyChanged(nameof(CanInstallChromium));
    }
}

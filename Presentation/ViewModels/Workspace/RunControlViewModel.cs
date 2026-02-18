using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        _backend.PropertyChanged += OnBackendPropertyChanged;
        _workspace.PropertyChanged += OnWorkspacePropertyChanged;
        _workspace.WorkspaceValidationErrors.CollectionChanged += (_, _) => RaiseState();
    }

    public IAsyncRelayCommand StartCommand { get; }
    public IRelayCommand StopCommand => _backend.StopCommand;
    public IRelayCommand OpenRunFolderCommand => _backend.OpenLatestRunFolderCommand;
    public IRelayCommand OpenHtmlReportCommand => _backend.OpenLatestHtmlCommand;
    public IRelayCommand OpenJsonReportCommand => _backend.OpenLatestJsonCommand;

    public string StatusText => _backend.StatusText;
    public double ProgressValue => _backend.ProgressPercent;
    public bool CanStart => StartCommand.CanExecute(null);
    public bool CanStop => _backend.StopCommand.CanExecute(null);

    public bool HasRunFolder => _backend.SelectedModule?.LastReport != null;
    public bool HasHtmlReport => !string.IsNullOrWhiteSpace(_backend.SelectedModule?.LastReport?.Artifacts.HtmlPath);
    public bool HasJsonReport => !string.IsNullOrWhiteSpace(_backend.SelectedModule?.LastReport?.Artifacts.JsonPath);

    public ObservableCollection<string> ValidationErrors => _workspace.WorkspaceValidationErrors;
    public bool HasValidationErrors => ValidationErrors.Count > 0;

    [ObservableProperty]
    private int focusRequestToken;

    public void RequestStartFocus()
    {
        FocusRequestToken++;
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
               && _workspace.WorkspaceValidationErrors.Count == 0
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

    private void RaiseState()
    {
        StartCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(HasRunFolder));
        OnPropertyChanged(nameof(HasHtmlReport));
        OnPropertyChanged(nameof(HasJsonReport));
        OnPropertyChanged(nameof(ValidationErrors));
        OnPropertyChanged(nameof(HasValidationErrors));
    }
}

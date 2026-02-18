using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class RunControlViewModel : ObservableObject
{
    private readonly MainWindowViewModel _backend;

    public RunControlViewModel(MainWindowViewModel backend)
    {
        _backend = backend;
        _backend.PropertyChanged += OnBackendPropertyChanged;
    }

    public IAsyncRelayCommand StartCommand => _backend.StartCommand;
    public IRelayCommand StopCommand => _backend.StopCommand;
    public IRelayCommand OpenRunFolderCommand => _backend.OpenLatestRunFolderCommand;
    public IRelayCommand OpenHtmlReportCommand => _backend.OpenLatestHtmlCommand;
    public IRelayCommand OpenJsonReportCommand => _backend.OpenLatestJsonCommand;

    public string StatusText => _backend.StatusText;
    public double ProgressValue => _backend.ProgressPercent;
    public bool CanStart => _backend.StartCommand.CanExecute(null);
    public bool CanStop => _backend.StopCommand.CanExecute(null);

    public bool HasRunFolder => _backend.SelectedModule?.LastReport != null;
    public bool HasHtmlReport => !string.IsNullOrWhiteSpace(_backend.SelectedModule?.LastReport?.Artifacts.HtmlPath);
    public bool HasJsonReport => !string.IsNullOrWhiteSpace(_backend.SelectedModule?.LastReport?.Artifacts.JsonPath);

    private void OnBackendPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(HasRunFolder));
        OnPropertyChanged(nameof(HasHtmlReport));
        OnPropertyChanged(nameof(HasJsonReport));
    }
}

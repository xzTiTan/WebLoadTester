using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class ModuleWorkspaceViewModel : ObservableObject
{
    private readonly MainWindowViewModel _backend;

    public ModuleWorkspaceViewModel(MainWindowViewModel backend, object? moduleSettingsVm)
    {
        _backend = backend;
        ModuleSettingsVm = moduleSettingsVm;
        IsRunning = backend.IsRunning;
        _backend.PropertyChanged += OnBackendPropertyChanged;
    }

    [ObservableProperty]
    private object? moduleSettingsVm;

    [ObservableProperty]
    private string moduleDisplayName = string.Empty;

    [ObservableProperty]
    private string moduleId = string.Empty;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private double leftNavWidth = 280;

    [ObservableProperty]
    private double detailsWidth = 340;

    [ObservableProperty]
    private bool isDetailsVisible = true;

    public object RunControlVm { get; } = "Run Control";
    public object TestCaseVm { get; } = "TestCase";
    public object RunProfileVm { get; } = "RunProfile";

    public bool IsIdle => !IsRunning;

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIdle));
    }

    private void OnBackendPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsRunning))
        {
            IsRunning = _backend.IsRunning;
        }
    }
}

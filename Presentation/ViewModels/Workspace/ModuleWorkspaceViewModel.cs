using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class ModuleWorkspaceViewModel : ObservableObject
{
    private readonly MainWindowViewModel _backend;

    public ModuleWorkspaceViewModel(MainWindowViewModel backend, LogDrawerViewModel logDrawer)
    {
        _backend = backend;
        IsRunning = backend.IsRunning;
        _backend.PropertyChanged += OnBackendPropertyChanged;

        RunControl = new RunControlViewModel(_backend);
        TestCase = new TestCaseViewModel(_backend);
        RunProfile = new RunProfileViewModel(_backend.RunProfile);
        Details = new DetailsPaneViewModel(_backend, logDrawer, this);
    }

    public RunControlViewModel RunControl { get; }
    public TestCaseViewModel TestCase { get; }
    public RunProfileViewModel RunProfile { get; }
    public DetailsPaneViewModel Details { get; }

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

    public bool IsIdle => !IsRunning;

    public void SetSelectedModule(ModuleDescriptorVm? descriptor)
    {
        ModuleSettingsVm = descriptor?.ModuleSettingsVm;
        ModuleDisplayName = descriptor?.DisplayName ?? string.Empty;
        ModuleId = descriptor?.ModuleId ?? string.Empty;
        TestCase.SetModuleConfig(descriptor?.ModuleConfig);
        RunProfile.IsUiFamily = descriptor?.ModuleId.StartsWith("ui.") == true;
        RunProfile.RefreshAll();
    }

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

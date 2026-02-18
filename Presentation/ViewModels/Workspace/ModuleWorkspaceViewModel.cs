using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WebLoadTester.Presentation.Common;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class ModuleWorkspaceViewModel : ObservableObject
{
    private readonly MainWindowViewModel _backend;
    private INotifyPropertyChanged? _currentValidatableSettings;

    public ModuleWorkspaceViewModel(MainWindowViewModel backend, LogDrawerViewModel logDrawer)
    {
        _backend = backend;
        IsRunning = backend.IsRunning;
        _backend.PropertyChanged += OnBackendPropertyChanged;

        WorkspaceValidationErrors = new ObservableCollection<string>();
        WorkspaceValidationErrors.CollectionChanged += OnWorkspaceValidationErrorsChanged;

        RunControl = new RunControlViewModel(_backend, this);
        TestCase = new TestCaseViewModel(_backend);
        RunProfile = new RunProfileViewModel(_backend.RunProfile);
        Details = new DetailsPaneViewModel(_backend, logDrawer, this);

        TestCase.PropertyChanged += OnChildValidationSourceChanged;
        RunProfile.PropertyChanged += OnChildValidationSourceChanged;

        RefreshWorkspaceValidationErrors();
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

    public ObservableCollection<string> WorkspaceValidationErrors { get; }

    public bool HasWorkspaceValidationErrors => WorkspaceValidationErrors.Count > 0;

    public bool IsIdle => !IsRunning;

    public void SetSelectedModule(ModuleDescriptorVm? descriptor)
    {
        UnsubscribeSettingsValidationSource();

        ModuleSettingsVm = descriptor?.ModuleSettingsVm;
        ModuleDisplayName = descriptor?.DisplayName ?? string.Empty;
        ModuleId = descriptor?.ModuleId ?? string.Empty;
        TestCase.SetModuleConfig(descriptor?.ModuleConfig);
        RunProfile.IsUiFamily = descriptor?.ModuleId.StartsWith("ui.") == true;
        RunProfile.RefreshAll();

        SubscribeSettingsValidationSource();
        RefreshWorkspaceValidationErrors();
    }

    public IReadOnlyList<string> GetWorkspaceValidationErrors()
    {
        var errors = new List<string>();

        errors.AddRange(TestCase.Validate());
        errors.AddRange(RunProfile.Validate());

        if (ModuleSettingsVm is IValidatable validatable)
        {
            errors.AddRange(validatable.Validate());
        }

        return errors
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
    }

    public void RefreshWorkspaceValidationErrors()
    {
        var next = GetWorkspaceValidationErrors();

        WorkspaceValidationErrors.Clear();
        foreach (var error in next)
        {
            WorkspaceValidationErrors.Add(error);
        }

        OnPropertyChanged(nameof(WorkspaceValidationErrors));
        OnPropertyChanged(nameof(HasWorkspaceValidationErrors));
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

    private void OnChildValidationSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshWorkspaceValidationErrors();
    }

    private void SubscribeSettingsValidationSource()
    {
        if (ModuleSettingsVm is INotifyPropertyChanged notifyPropertyChanged)
        {
            _currentValidatableSettings = notifyPropertyChanged;
            _currentValidatableSettings.PropertyChanged += OnChildValidationSourceChanged;
        }
    }

    private void UnsubscribeSettingsValidationSource()
    {
        if (_currentValidatableSettings == null)
        {
            return;
        }

        _currentValidatableSettings.PropertyChanged -= OnChildValidationSourceChanged;
        _currentValidatableSettings = null;
    }

    private void OnWorkspaceValidationErrorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasWorkspaceValidationErrors));
    }
}

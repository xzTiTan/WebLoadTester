using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Infrastructure.Storage;
using WebLoadTester.Presentation.Common;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class ModuleWorkspaceViewModel : ObservableObject
{
    private readonly MainWindowViewModel _backend;
    private readonly Action? _onLayoutStateChanged;
    private INotifyPropertyChanged? _currentValidatableSettings;

    public ModuleWorkspaceViewModel(MainWindowViewModel backend, LogDrawerViewModel logDrawer, UiLayoutState? initialState = null, Action? onLayoutStateChanged = null)
    {
        _backend = backend;
        _onLayoutStateChanged = onLayoutStateChanged;
        IsRunning = backend.IsRunning;
        _backend.PropertyChanged += OnBackendPropertyChanged;

        if (initialState != null)
        {
            leftNavWidth = initialState.LeftNavWidth;
            detailsWidth = initialState.DetailsWidth;
            isDetailsVisible = initialState.IsDetailsVisible;
            isTestCaseExpanded = initialState.IsTestCaseExpanded;
            isRunProfileExpanded = initialState.IsRunProfileExpanded;
            isModuleSettingsExpanded = initialState.IsModuleSettingsExpanded;
        }

        WorkspaceValidationErrors = new ObservableCollection<string>();
        WorkspaceValidationErrors.CollectionChanged += OnWorkspaceValidationErrorsChanged;

        RunControl = new RunControlViewModel(_backend, this);
        TestCase = new TestCaseViewModel(_backend);
        RunProfile = new RunProfileViewModel(_backend.RunProfile);
        Details = new DetailsPaneViewModel(_backend, logDrawer, this);

        TestCase.PropertyChanged += OnChildValidationSourceChanged;
        TestCase.PropertyChanged += OnTestCasePromptStateChanged;
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

    [ObservableProperty]
    private bool isTestCaseExpanded = true;

    [ObservableProperty]
    private bool isRunProfileExpanded = true;

    [ObservableProperty]
    private bool isModuleSettingsExpanded = true;

    [ObservableProperty]
    private int scrollToTopRequestToken;

    public ObservableCollection<string> WorkspaceValidationErrors { get; }

    public bool HasWorkspaceValidationErrors => WorkspaceValidationErrors.Count > 0;

    public bool IsIdle => !IsRunning;

    public bool HasModuleSelected => !string.IsNullOrWhiteSpace(ModuleId);

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
        OnPropertyChanged(nameof(HasModuleSelected));
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

    public void RequestRunControlFocus()
    {
        RunControl.RequestStartFocus();
    }

    public void RequestScrollToTop()
    {
        ScrollToTopRequestToken++;
    }

    [RelayCommand]
    private void ToggleDetails()
    {
        IsDetailsVisible = !IsDetailsVisible;
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIdle));
    }

    partial void OnLeftNavWidthChanged(double value) => _onLayoutStateChanged?.Invoke();
    partial void OnDetailsWidthChanged(double value) => _onLayoutStateChanged?.Invoke();
    partial void OnIsDetailsVisibleChanged(bool value) => _onLayoutStateChanged?.Invoke();
    partial void OnIsTestCaseExpandedChanged(bool value) => _onLayoutStateChanged?.Invoke();
    partial void OnIsRunProfileExpandedChanged(bool value) => _onLayoutStateChanged?.Invoke();
    partial void OnIsModuleSettingsExpandedChanged(bool value) => _onLayoutStateChanged?.Invoke();

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


    private void OnTestCasePromptStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TestCaseViewModel.IsDirtyPromptVisible) &&
            e.PropertyName != nameof(TestCaseViewModel.IsDeleteConfirmVisible))
        {
            return;
        }

        if (TestCase.IsDirtyPromptVisible || TestCase.IsDeleteConfirmVisible)
        {
            IsTestCaseExpanded = true;
            RequestScrollToTop();
        }
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

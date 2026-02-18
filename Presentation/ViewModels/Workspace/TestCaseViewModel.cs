using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Presentation.Common;
using WebLoadTester.Core.Domain;

namespace WebLoadTester.Presentation.ViewModels.Workspace;

public partial class TestCaseViewModel : ObservableObject, IValidatable
{
    private readonly MainWindowViewModel _backend;
    private ModuleConfigViewModel? _moduleConfig;

    public TestCaseViewModel(MainWindowViewModel backend)
    {
        _backend = backend;
    }

    public ObservableCollection<ModuleConfigSummary> Configs => _moduleConfig?.Configs ?? _empty;
    private static readonly ObservableCollection<ModuleConfigSummary> _empty = new();

    public ModuleConfigSummary? SelectedConfig
    {
        get => _moduleConfig?.SelectedConfig;
        set
        {
            if (_moduleConfig == null || ReferenceEquals(_moduleConfig.SelectedConfig, value))
            {
                return;
            }

            _moduleConfig.SelectedConfig = value;
            OnPropertyChanged();
        }
    }

    public string Name
    {
        get => _moduleConfig?.UserName ?? string.Empty;
        set
        {
            if (_moduleConfig == null || _moduleConfig.UserName == value)
            {
                return;
            }

            _moduleConfig.UserName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FinalNamePreview));
        }
    }

    public string Description
    {
        get => _moduleConfig?.Description ?? string.Empty;
        set
        {
            if (_moduleConfig == null || _moduleConfig.Description == value)
            {
                return;
            }

            _moduleConfig.Description = value;
            OnPropertyChanged();
        }
    }

    public string DirtyMark => _moduleConfig?.DirtyStateText ?? string.Empty;
    public bool IsDirty => _moduleConfig?.IsDirty ?? false;
    public string FinalNamePreview => _moduleConfig?.FinalNamePreview ?? string.Empty;

    public IEnumerable<string> ValidationErrors
    {
        get
        {
            if (_moduleConfig == null)
            {
                return [];
            }

            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(_moduleConfig.NameValidationMessage)) list.Add(_moduleConfig.NameValidationMessage);
            if (!string.IsNullOrWhiteSpace(_moduleConfig.ConfigSummaryMessage)) list.Add(_moduleConfig.ConfigSummaryMessage);
            if (!string.IsNullOrWhiteSpace(_moduleConfig.ModuleSummaryMessage)) list.Add(_moduleConfig.ModuleSummaryMessage);
            return list;
        }
    }

    public string StatusMessage => _moduleConfig?.StatusMessage ?? string.Empty;

    public IAsyncRelayCommand? LoadCommand => _moduleConfig?.LoadSelectedCommand;
    public IAsyncRelayCommand? SaveCommand => _moduleConfig?.SaveCommand;
    public IAsyncRelayCommand? SaveAsCommand => _moduleConfig?.SaveCommand;
    public IRelayCommand? DeleteCommand => _moduleConfig?.RequestDeleteSelectedCommand;

    public IReadOnlyList<string> Validate()
    {
        return ValidationErrors
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();
    }

    public void SetModuleConfig(ModuleConfigViewModel? moduleConfig)
    {
        if (_moduleConfig != null)
        {
            _moduleConfig.PropertyChanged -= OnModuleConfigPropertyChanged;
        }

        _moduleConfig = moduleConfig;

        if (_moduleConfig != null)
        {
            _moduleConfig.PropertyChanged += OnModuleConfigPropertyChanged;
        }

        RaiseAll();
    }

    private void OnModuleConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaiseAll();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Configs));
        OnPropertyChanged(nameof(SelectedConfig));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(DirtyMark));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(FinalNamePreview));
        OnPropertyChanged(nameof(ValidationErrors));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(LoadCommand));
        OnPropertyChanged(nameof(SaveCommand));
        OnPropertyChanged(nameof(SaveAsCommand));
        OnPropertyChanged(nameof(DeleteCommand));
    }
}

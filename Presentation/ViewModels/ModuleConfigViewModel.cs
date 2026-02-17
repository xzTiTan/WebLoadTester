using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;

namespace WebLoadTester.Presentation.ViewModels;

public partial class ModuleConfigViewModel : ObservableObject
{
    private static readonly Regex NameRegex = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    public const string ConfigNameKey = "config.name";
    public const string ConfigSummaryKey = "config.summary";
    public const string ModuleSummaryKey = "module.summary";
    public const string TableStepsKey = "table.steps";
    public const string TableTargetsKey = "table.targets";
    public const string TableAssetsKey = "table.assets";
    public const string TablePortsKey = "table.ports";
    public const string ListEndpointsKey = "list.endpoints";

    private readonly IModuleConfigService _configService;
    private readonly ITestCaseRepository _testCaseRepository;
    private readonly ITestModule _module;
    private readonly SettingsViewModelBase _settings;
    private readonly RunProfileViewModel _runProfile;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private bool _suppressDirtyTracking;
    private bool _suppressSelectionGuard;
    private ModuleConfigSummary? _lastSelectedConfig;
    private Func<Task>? _pendingGuardAction;

    public ModuleConfigViewModel(IModuleConfigService configService, ITestCaseRepository testCaseRepository, ITestModule module, SettingsViewModelBase settings, RunProfileViewModel runProfile)
    {
        _configService = configService;
        _testCaseRepository = testCaseRepository;
        _module = module;
        _settings = settings;
        _runProfile = runProfile;

        _settings.PropertyChanged += (_, _) =>
        {
            MarkDirty();
            RevalidateModuleSettings();
        };
        _runProfile.PropertyChanged += (_, _) => MarkDirty();

        RevalidateConfigAndModule();
    }

    public ObservableCollection<ModuleConfigSummary> Configs { get; } = new();

    [ObservableProperty] private ModuleConfigSummary? selectedConfig;
    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isDeleteConfirmVisible;
    [ObservableProperty] private bool isDirty;
    [ObservableProperty] private bool isDirtyPromptVisible;
    [ObservableProperty] private string dirtyPromptText = string.Empty;
    [ObservableProperty] private string nameValidationMessage = string.Empty;
    [ObservableProperty] private string requestedScrollToValidationKey = string.Empty;
    [ObservableProperty] private int requestedScrollNonce;

    public ValidationState ConfigValidation { get; } = new();
    public ValidationState ModuleValidation { get; } = new();

    public string DirtyStateText => IsDirty ? "Есть несохранённые изменения" : "Сохранено";
    public bool HasNameValidationMessage => !string.IsNullOrWhiteSpace(NameValidationMessage);
    public bool HasConfigSummaryError => !string.IsNullOrWhiteSpace(ConfigSummaryMessage);
    public bool HasModuleSummaryError => !string.IsNullOrWhiteSpace(ModuleSummaryMessage);
    public string ConfigSummaryMessage => ConfigValidation.GetVisibleError(ConfigSummaryKey);
    public string ModuleSummaryMessage => ModuleValidation.GetVisibleError(ModuleSummaryKey);

    public string FinalNamePreview
    {
        get
        {
            var normalized = NormalizeUserName(UserName);
            return string.IsNullOrWhiteSpace(normalized) ? string.Empty : $"{normalized}_{ModuleCatalog.GetSuffix(_module.Id)}";
        }
    }

    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(DirtyStateText));
    partial void OnNameValidationMessageChanged(string value) => OnPropertyChanged(nameof(HasNameValidationMessage));
    partial void OnIsBusyChanged(bool value) => SaveCommand.NotifyCanExecuteChanged();

    partial void OnSelectedConfigChanged(ModuleConfigSummary? value)
    {
        if (_suppressSelectionGuard)
        {
            _lastSelectedConfig = value;
            return;
        }

        if (value == null || _lastSelectedConfig == null || ReferenceEquals(value, _lastSelectedConfig))
        {
            _lastSelectedConfig = value;
            return;
        }

        if (!IsDirty)
        {
            _lastSelectedConfig = value;
            return;
        }

        _suppressSelectionGuard = true;
        SelectedConfig = _lastSelectedConfig;
        _suppressSelectionGuard = false;

        RequestDirtyGuard(async () =>
        {
            _suppressSelectionGuard = true;
            SelectedConfig = value;
            _suppressSelectionGuard = false;
            _lastSelectedConfig = value;
            await LoadSelectedCoreAsync();
        });
    }

    partial void OnUserNameChanged(string value)
    {
        OnPropertyChanged(nameof(FinalNamePreview));
        SaveCommand.NotifyCanExecuteChanged();
        RevalidateConfigAndModule();
        if (_suppressDirtyTracking)
        {
            return;
        }

        MarkDirty();
    }

    partial void OnDescriptionChanged(string value)
    {
        RevalidateConfigAndModule();
        if (_suppressDirtyTracking)
        {
            return;
        }

        MarkDirty();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var items = await _configService.ListAsync(_module.Id, CancellationToken.None);
            Configs.Clear();
            foreach (var item in items.OrderByDescending(i => i.UpdatedAt))
            {
                Configs.Add(item);
            }

            if (SelectedConfig == null && Configs.Count > 0)
            {
                _suppressSelectionGuard = true;
                SelectedConfig = Configs[0];
                _suppressSelectionGuard = false;
                _lastSelectedConfig = SelectedConfig;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadSelectedAsync()
    {
        if (IsDirty)
        {
            RequestDirtyGuard(LoadSelectedCoreAsync);
            return;
        }

        await LoadSelectedCoreAsync();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ShowSubmitValidation();
        if (!ValidateSettings())
        {
            return;
        }

        try
        {
            var finalName = await _configService.SaveNewAsync(UserName, _module.Id, Description, _settings.Settings, _runProfile.BuildRunParameters(), CancellationToken.None);
            await RefreshAsync();
            _suppressSelectionGuard = true;
            SelectedConfig = Configs.FirstOrDefault(item => string.Equals(item.FinalName, finalName, StringComparison.OrdinalIgnoreCase));
            _suppressSelectionGuard = false;
            _lastSelectedConfig = SelectedConfig;
            IsDirty = false;
            StatusMessage = $"Конфигурация сохранена: {finalName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения конфигурации: {ex.Message}";
        }
    }

    [RelayCommand]
    private void MarkFieldTouched(string key)
    {
        ConfigValidation.MarkTouched(key);
        ModuleValidation.MarkTouched(key);
        RefreshValidationViews();
    }

    [RelayCommand]
    private void RequestDeleteSelected()
    {
        if (SelectedConfig == null)
        {
            return;
        }

        IsDeleteConfirmVisible = true;
        StatusMessage = $"Удалить конфигурацию \"{SelectedConfig.FinalName}\"?";
    }

    [RelayCommand]
    private async Task ConfirmDeleteSelectedAsync()
    {
        if (SelectedConfig == null)
        {
            return;
        }

        await _configService.DeleteAsync(SelectedConfig.FinalName, CancellationToken.None);
        IsDeleteConfirmVisible = false;
        _suppressSelectionGuard = true;
        SelectedConfig = null;
        _suppressSelectionGuard = false;
        _lastSelectedConfig = null;
        StatusMessage = "Конфигурация удалена.";
        await RefreshAsync();
    }

    [RelayCommand] private void CancelDeleteSelected() => IsDeleteConfirmVisible = false;

    [RelayCommand]
    private async Task SaveAndContinueGuardActionAsync()
    {
        await SaveAsync();
        if (IsDirty || !string.IsNullOrWhiteSpace(NameValidationMessage) || _pendingGuardAction == null)
        {
            return;
        }

        var action = _pendingGuardAction;
        _pendingGuardAction = null;
        IsDirtyPromptVisible = false;
        await action();
    }

    [RelayCommand]
    private async Task DiscardAndContinueGuardActionAsync()
    {
        IsDirty = false;
        IsDirtyPromptVisible = false;
        var action = _pendingGuardAction;
        _pendingGuardAction = null;
        if (action != null)
        {
            await action();
        }
    }

    [RelayCommand]
    private void CancelGuardAction()
    {
        IsDirtyPromptVisible = false;
        _pendingGuardAction = null;
    }

    public bool TryRequestLeave(Func<Task> continueAction)
    {
        if (!IsDirty)
        {
            _ = continueAction();
            return true;
        }

        RequestDirtyGuard(continueAction);
        return false;
    }

    public async Task<TestCase?> EnsureConfigForRunAsync()
    {
        if (SelectedConfig == null)
        {
            if (string.IsNullOrWhiteSpace(UserName))
            {
                StatusMessage = "Укажите имя конфигурации перед запуском.";
                return null;
            }
        }

        await SaveAsync();
        if (SelectedConfig == null)
        {
            return null;
        }

        return await _testCaseRepository.GetByNameAsync(SelectedConfig.FinalName, CancellationToken.None);
    }

    public void MarkCleanFromExternalLoad()
    {
        IsDirty = false;
        IsDirtyPromptVisible = false;
        ConfigValidation.ResetVisibility();
        ModuleValidation.ResetVisibility();
        RevalidateConfigAndModule();
    }

    private async Task LoadSelectedCoreAsync()
    {
        if (SelectedConfig == null)
        {
            StatusMessage = "Выберите конфигурацию для загрузки.";
            return;
        }

        var payload = await _configService.LoadAsync(SelectedConfig.FinalName, CancellationToken.None);
        if (payload == null)
        {
            StatusMessage = "Не удалось загрузить выбранную конфигурацию.";
            return;
        }

        _suppressDirtyTracking = true;
        try
        {
            if (payload.ModuleSettings.ValueKind != JsonValueKind.Undefined)
            {
                var moduleSettings = JsonSerializer.Deserialize(payload.ModuleSettings.GetRawText(), _module.SettingsType, _jsonOptions);
                if (moduleSettings != null)
                {
                    _settings.UpdateFrom(moduleSettings);
                }
            }

            _runProfile.UpdateFrom(payload.RunParameters);
            UserName = payload.Meta.UserName;
            Description = payload.Meta.Description;
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        ConfigValidation.ResetVisibility();
        ModuleValidation.ResetVisibility();
        RevalidateConfigAndModule();
        IsDirty = false;
        _lastSelectedConfig = SelectedConfig;
        StatusMessage = "Конфигурация загружена.";
    }

    private void RequestDirtyGuard(Func<Task> continueAction)
    {
        _pendingGuardAction = continueAction;
        DirtyPromptText = "Есть несохранённые изменения. Сохранить?";
        IsDirtyPromptVisible = true;
    }

    private void MarkDirty()
    {
        if (_suppressDirtyTracking)
        {
            return;
        }

        IsDirty = true;
    }

    public void ShowSubmitValidation()
    {
        ConfigValidation.ShowAll();
        ModuleValidation.ShowAll();
        RevalidateConfigAndModule();
    }

    public string? GetFirstVisibleValidationKey()
    {
        var key = ConfigValidation.GetFirstVisibleErrorKey(new[]
        {
            ConfigNameKey,
            ConfigSummaryKey
        });
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        return ModuleValidation.GetFirstVisibleErrorKey(new[]
        {
            TableStepsKey,
            TableTargetsKey,
            ListEndpointsKey,
            TableAssetsKey,
            TablePortsKey,
            ModuleSummaryKey
        });
    }

    private bool ValidateSettings()
    {
        RevalidateConfigAndModule();
        if (ConfigValidation.HasErrors || ModuleValidation.HasErrors)
        {
            var errors = ConfigValidation.ErrorsByKey.Values.Concat(ModuleValidation.ErrorsByKey.Values).ToList();
            StatusMessage = "Заполните обязательные поля: " + string.Join("; ", errors);
            RequestScrollToFirstError();
            return false;
        }

        return true;
    }

    private bool CanSave()
    {
        return !IsBusy && string.IsNullOrWhiteSpace(BuildConfigErrors()[ConfigNameKey]);
    }

    private void RevalidateConfigAndModule()
    {
        ConfigValidation.SetErrors(BuildConfigErrors());
        ModuleValidation.SetErrors(BuildModuleErrors());
        RefreshValidationViews();
    }

    private void RevalidateModuleSettings()
    {
        ModuleValidation.SetErrors(BuildModuleErrors());
        RefreshValidationViews();
    }

    private Dictionary<string, string> BuildConfigErrors()
    {
        var errors = new Dictionary<string, string>();
        var normalized = NormalizeUserName(UserName);
        if (string.IsNullOrWhiteSpace(normalized) || !NameRegex.IsMatch(normalized))
        {
            errors[ConfigNameKey] = "Имя должно содержать только A-Z, a-z, 0-9, _ или - (без пробелов).";
            errors[ConfigSummaryKey] = errors[ConfigNameKey];
        }
        else
        {
            errors[ConfigNameKey] = string.Empty;
        }

        return errors;
    }

    private Dictionary<string, string> BuildModuleErrors()
    {
        var errors = new Dictionary<string, string>();
        var moduleErrors = _module.Validate(_settings.Settings);
        if (moduleErrors.Count > 0)
        {
            errors[ModuleSummaryKey] = "Есть ошибки: " + string.Join("; ", moduleErrors);
            var tableKey = TryGetTableValidationKey(moduleErrors);
            if (!string.IsNullOrWhiteSpace(tableKey))
            {
                errors[tableKey] = moduleErrors[0];
            }
        }

        return errors;
    }

    private void RefreshValidationViews()
    {
        NameValidationMessage = ConfigValidation.GetVisibleError(ConfigNameKey);
        OnPropertyChanged(nameof(ConfigSummaryMessage));
        OnPropertyChanged(nameof(ModuleSummaryMessage));
        OnPropertyChanged(nameof(HasConfigSummaryError));
        OnPropertyChanged(nameof(HasModuleSummaryError));
    }

    private void RequestScrollToFirstError()
    {
        var key = GetFirstVisibleValidationKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        RequestedScrollToValidationKey = key;
        RequestedScrollNonce++;
    }

    private string? TryGetTableValidationKey(IReadOnlyList<string> errors)
    {
        if (errors.Count == 0)
        {
            return null;
        }

        return _module.Id switch
        {
            "ui.scenario" when errors.Any(e => e.Contains("шаг", StringComparison.OrdinalIgnoreCase)) => TableStepsKey,
            "ui.snapshot" or "ui.timing" when errors.Any(e => e.Contains("цель", StringComparison.OrdinalIgnoreCase)) => TableTargetsKey,
            "http.assets" when errors.Any(e => e.Contains("Assets", StringComparison.OrdinalIgnoreCase) || e.Contains("ассет", StringComparison.OrdinalIgnoreCase)) => TableAssetsKey,
            "net.diagnostics" when errors.Any(e => e.Contains("порт", StringComparison.OrdinalIgnoreCase)) => TablePortsKey,
            "http.functional" or "http.performance" when errors.Any(e => e.Contains("Endpoints", StringComparison.OrdinalIgnoreCase) || e.Contains("Endpoint", StringComparison.OrdinalIgnoreCase)) => ListEndpointsKey,
            _ => null
        };
    }

    private static string NormalizeUserName(string value)
    {
        return value.Trim();
    }
}

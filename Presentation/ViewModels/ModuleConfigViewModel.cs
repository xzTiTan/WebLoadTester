using System;
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

        _settings.PropertyChanged += (_, _) => MarkDirty();
        _runProfile.PropertyChanged += (_, _) => MarkDirty();
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

    public string DirtyStateText => IsDirty ? "Есть несохранённые изменения" : "Сохранено";
    public bool HasNameValidationMessage => !string.IsNullOrWhiteSpace(NameValidationMessage);

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
        if (_suppressDirtyTracking)
        {
            return;
        }

        MarkDirty();
    }

    partial void OnDescriptionChanged(string value)
    {
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

    [RelayCommand]
    private async Task SaveAsync()
    {
        NameValidationMessage = string.Empty;
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

        NameValidationMessage = string.Empty;
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

    private bool ValidateSettings()
    {
        var normalized = NormalizeUserName(UserName);
        if (string.IsNullOrWhiteSpace(normalized) || !NameRegex.IsMatch(normalized))
        {
            NameValidationMessage = "Имя должно содержать только A-Z, a-z, 0-9, _ или - (без пробелов).";
            StatusMessage = NameValidationMessage;
            return false;
        }

        var errors = _module.Validate(_settings.Settings);
        if (errors.Count > 0)
        {
            StatusMessage = "Заполните обязательные поля: " + string.Join("; ", errors);
            return false;
        }

        return true;
    }

    private static string NormalizeUserName(string value)
    {
        return value.Trim();
    }
}

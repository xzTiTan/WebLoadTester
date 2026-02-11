using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// ViewModel управления конфигурациями модуля.
/// </summary>
public partial class ModuleConfigViewModel : ObservableObject
{
    private readonly IModuleConfigService _configService;
    private readonly ITestCaseRepository _testCaseRepository;
    private readonly ITestModule _module;
    private readonly SettingsViewModelBase _settings;
    private readonly RunProfileViewModel _runProfile;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ModuleConfigViewModel(IModuleConfigService configService, ITestCaseRepository testCaseRepository, ITestModule module, SettingsViewModelBase settings, RunProfileViewModel runProfile)
    {
        ArgumentNullException.ThrowIfNull(configService);
        ArgumentNullException.ThrowIfNull(testCaseRepository);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(runProfile);

        _configService = configService;
        _testCaseRepository = testCaseRepository;
        _module = module;
        _settings = settings;
        _runProfile = runProfile;
        userName = string.Empty;
    }

    public ObservableCollection<ModuleConfigSummary> Configs { get; } = new();

    [ObservableProperty]
    private ModuleConfigSummary? selectedConfig;

    partial void OnSelectedConfigChanged(ModuleConfigSummary? value)
    {
        if (value == null)
        {
            return;
        }

        Description = value.Description;
        UserName = ParseUserName(value.FinalName);
    }

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isDeleteConfirmVisible;

    public string FinalNamePreview
    {
        get
        {
            var normalized = NormalizeUserName(UserName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            return $"{normalized}_{ModuleCatalog.GetSuffix(_module.Id)}";
        }
    }

    partial void OnUserNameChanged(string value) => OnPropertyChanged(nameof(FinalNamePreview));

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            Configs.Clear();
            var items = await _configService.ListAsync(_module.Id, CancellationToken.None);
            foreach (var item in items.OrderByDescending(item => item.UpdatedAt))
            {
                Configs.Add(item);
            }

            if (SelectedConfig == null && Configs.Count > 0)
            {
                SelectedConfig = Configs[0];
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
        StatusMessage = "Конфигурация загружена.";
    }

    [RelayCommand]
    private async Task SaveNewAsync()
    {
        if (!ValidateSettings())
        {
            return;
        }

        try
        {
            var finalName = await _configService.SaveNewAsync(UserName, _module.Id, Description, _settings.Settings, _runProfile.BuildRunParameters(), CancellationToken.None);
            await RefreshAsync();
            SelectedConfig = Configs.FirstOrDefault(item => string.Equals(item.FinalName, finalName, StringComparison.OrdinalIgnoreCase));
            StatusMessage = $"Конфигурация сохранена как {finalName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения конфигурации: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveOverwriteAsync()
    {
        if (SelectedConfig == null)
        {
            StatusMessage = "Выберите конфигурацию для перезаписи.";
            return;
        }

        if (!ValidateSettings())
        {
            return;
        }

        await _configService.SaveOverwriteAsync(SelectedConfig.FinalName, _module.Id, Description, _settings.Settings, _runProfile.BuildRunParameters(), CancellationToken.None);
        await RefreshAsync();
        SelectedConfig = Configs.FirstOrDefault(item => string.Equals(item.FinalName, SelectedConfig.FinalName, StringComparison.OrdinalIgnoreCase));
        StatusMessage = "Конфигурация обновлена.";
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
        SelectedConfig = null;
        StatusMessage = "Конфигурация удалена.";
        await RefreshAsync();
    }

    [RelayCommand]
    private void CancelDeleteSelected()
    {
        IsDeleteConfirmVisible = false;
        StatusMessage = string.Empty;
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

            await SaveNewAsync();
        }

        if (SelectedConfig == null)
        {
            return null;
        }

        return await _testCaseRepository.GetByNameAsync(SelectedConfig.FinalName, CancellationToken.None);
    }

    private bool ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(UserName) || UserName.Any(char.IsWhiteSpace))
        {
            StatusMessage = "Имя конфигурации должно быть без пробелов.";
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
        return string.Join(string.Empty, value.Where(ch => !char.IsWhiteSpace(ch))).Trim();
    }

    private static string ParseUserName(string finalName)
    {
        var index = finalName.IndexOf('_', StringComparison.Ordinal);
        return index > 0 ? finalName[..index] : finalName;
    }
}

using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebLoadTester.Infrastructure.Storage;
using WebLoadTester.Infrastructure.Telegram;

namespace WebLoadTester.Presentation.ViewModels;

/// <summary>
/// ViewModel окна настроек приложения.
/// </summary>
public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly AppSettingsService _settingsService;

    public SettingsWindowViewModel(AppSettingsService settingsService, TelegramSettingsViewModel telegramSettings)
    {
        _settingsService = settingsService;
        TelegramSettings = telegramSettings;
        dataDirectory = settingsService.Settings.DataDirectory;
        runsDirectory = settingsService.Settings.RunsDirectory;
        profilesDirectory = settingsService.Settings.ProfilesDirectory;
    }

    public TelegramSettingsViewModel TelegramSettings { get; }

    [ObservableProperty]
    private string dataDirectory = string.Empty;

    [ObservableProperty]
    private string runsDirectory = string.Empty;

    [ObservableProperty]
    private string profilesDirectory = string.Empty;

    [RelayCommand]
    private async Task SaveAsync()
    {
        _settingsService.Settings.DataDirectory = DataDirectory;
        _settingsService.Settings.RunsDirectory = RunsDirectory;
        _settingsService.Settings.ProfilesDirectory = ProfilesDirectory;
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(RunsDirectory);
        Directory.CreateDirectory(ProfilesDirectory);
        await _settingsService.SaveAsync();
    }
}

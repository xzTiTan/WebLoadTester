using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
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

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public string DatabasePath => Path.Combine(DataDirectory, "webloadtester.db");

    partial void OnDataDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(DatabasePath));
    }

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
        StatusMessage = "Настройки сохранены.";
    }

    [RelayCommand]
    private async Task BrowseDataDirectoryAsync()
    {
        var selected = await PickFolderAsync("Выберите папку для данных");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            DataDirectory = selected;
        }
    }

    [RelayCommand]
    private async Task BrowseRunsDirectoryAsync()
    {
        var selected = await PickFolderAsync("Выберите папку для прогонов");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            RunsDirectory = selected;
        }
    }

    [RelayCommand]
    private async Task BrowseProfilesDirectoryAsync()
    {
        var selected = await PickFolderAsync("Выберите папку для профилей");
        if (!string.IsNullOrWhiteSpace(selected))
        {
            ProfilesDirectory = selected;
        }
    }

    [RelayCommand]
    private void OpenDataDirectory()
    {
        OpenPath(DataDirectory);
    }

    [RelayCommand]
    private void OpenRunsDirectory()
    {
        OpenPath(RunsDirectory);
    }

    [RelayCommand]
    private void OpenProfilesDirectory()
    {
        OpenPath(ProfilesDirectory);
    }

    [RelayCommand]
    private void OpenDatabasePath()
    {
        OpenPath(DatabasePath);
    }

    [RelayCommand]
    private async Task CopyDataDirectoryAsync()
    {
        await CopyToClipboardAsync(DataDirectory);
    }

    [RelayCommand]
    private async Task CopyRunsDirectoryAsync()
    {
        await CopyToClipboardAsync(RunsDirectory);
    }

    [RelayCommand]
    private async Task CopyProfilesDirectoryAsync()
    {
        await CopyToClipboardAsync(ProfilesDirectory);
    }

    [RelayCommand]
    private async Task CopyDatabasePathAsync()
    {
        await CopyToClipboardAsync(DatabasePath);
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow == null)
        {
            StatusMessage = "Не удалось открыть диалог выбора папки.";
            return null;
        }

        var storageProvider = desktop.MainWindow.StorageProvider;
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        return folder?.Path.LocalPath;
    }

    private async Task CopyToClipboardAsync(string path)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard is IClipboard clipboard)
        {
            await clipboard.SetTextAsync(path);
            StatusMessage = "Путь скопирован в буфер обмена.";
        }
    }

    private void OpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!Directory.Exists(path) && !File.Exists(path)))
        {
            StatusMessage = "Указанный путь не найден.";
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        };
        Process.Start(psi);
    }
}

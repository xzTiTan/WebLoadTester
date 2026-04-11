using System;
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
    private SettingsSnapshot _initialSnapshot;

    public SettingsWindowViewModel(AppSettingsService settingsService, TelegramSettingsViewModel telegramSettings)
    {
        _settingsService = settingsService;
        TelegramSettings = telegramSettings;
        dataDirectory = settingsService.Settings.DataDirectory;
        runsDirectory = settingsService.Settings.RunsDirectory;
        _initialSnapshot = CaptureSnapshot();
    }

    public TelegramSettingsViewModel TelegramSettings { get; }

    public event Action? CloseRequested;

    [ObservableProperty]
    private string dataDirectory = string.Empty;

    [ObservableProperty]
    private string runsDirectory = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public string ValidationMessage => BuildValidationMessage();

    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

    public bool CanSave => !HasValidationMessage;

    public string DatabasePath =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? string.Empty
            : Path.Combine(DataDirectory, "webloadtester.db");

    partial void OnDataDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(DatabasePath));
        NotifyPathStateChanged();
    }

    partial void OnRunsDirectoryChanged(string value)
    {
        NotifyPathStateChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var dataPath = DataDirectory.Trim();
        var runsPath = RunsDirectory.Trim();

        if (!CanSave)
        {
            StatusMessage = ValidationMessage;
            return;
        }

        try
        {
            Directory.CreateDirectory(dataPath);
            Directory.CreateDirectory(runsPath);

            _settingsService.Settings.DataDirectory = dataPath;
            _settingsService.Settings.RunsDirectory = runsPath;
            _settingsService.Settings.BrowsersDirectory = Path.Combine(dataPath, "browsers");
            _settingsService.Settings.Telegram = TelegramSettings.Settings;

            await _settingsService.SaveAsync();

            DataDirectory = dataPath;
            RunsDirectory = runsPath;
            _initialSnapshot = CaptureSnapshot();
            StatusMessage = "Настройки сохранены.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось сохранить настройки: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RestoreSnapshot(_initialSnapshot);
        StatusMessage = string.Empty;
        CloseRequested?.Invoke();
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
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusMessage = "Пустой путь, копировать нечего.";
            return;
        }

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

    private void NotifyPathStateChanged()
    {
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationMessage));
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    private string BuildValidationMessage()
    {
        if (string.IsNullOrWhiteSpace(DataDirectory))
        {
            return "Укажите каталог данных.";
        }

        if (string.IsNullOrWhiteSpace(RunsDirectory))
        {
            return "Укажите каталог прогонов.";
        }

        return string.Empty;
    }

    private SettingsSnapshot CaptureSnapshot()
    {
        return new SettingsSnapshot(
            DataDirectory,
            RunsDirectory,
            TelegramSettings.CaptureSnapshot());
    }

    private void RestoreSnapshot(SettingsSnapshot snapshot)
    {
        DataDirectory = snapshot.DataDirectory;
        RunsDirectory = snapshot.RunsDirectory;
        TelegramSettings.RestoreSnapshot(snapshot.Telegram);
    }
}

public readonly record struct SettingsSnapshot(
    string DataDirectory,
    string RunsDirectory,
    TelegramSettingsSnapshot Telegram);

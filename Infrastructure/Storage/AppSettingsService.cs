using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WebLoadTester.Infrastructure.Telegram;

namespace WebLoadTester.Infrastructure.Storage;

/// <summary>
/// Сервис чтения/сохранения настроек приложения.
/// </summary>
public class AppSettingsService
{
    private readonly string _settingsPath;
    private readonly AppStorageLayout _storageLayout;

    public AppSettingsService()
    {
        _storageLayout = AppStoragePathResolver.Resolve();
        Directory.CreateDirectory(_storageLayout.RootDirectory);
        _settingsPath = _storageLayout.SettingsPath;
        Settings = Load();
        NormalizeSettings(Settings, _storageLayout);
        EnsureDirectories(Settings);
    }

    public AppSettings Settings { get; private set; }
    public AppStorageLayout StorageLayout => _storageLayout;
    public string SettingsPath => _settingsPath;

    public Task SaveAsync()
    {
        NormalizeSettings(Settings, _storageLayout);
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        EnsureDirectories(Settings);
        var settingsDirectory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(settingsDirectory))
        {
            Directory.CreateDirectory(settingsDirectory);
        }

        return File.WriteAllTextAsync(_settingsPath, json);
    }

    private AppSettings Load()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    return loaded;
                }
            }
            catch
            {
                // fallback to defaults when settings file is corrupted
            }
        }

        return AppSettings.CreateDefault(_storageLayout);
    }

    private static void EnsureDirectories(AppSettings settings)
    {
        Directory.CreateDirectory(settings.DataDirectory);
        Directory.CreateDirectory(settings.RunsDirectory);
        Directory.CreateDirectory(settings.BrowsersDirectory);
    }

    private static void NormalizeSettings(AppSettings settings, AppStorageLayout storageLayout)
    {
        settings.Telegram ??= new TelegramSettings();
        settings.UiLayout ??= new UiLayoutState();
        settings.DataDirectory = NormalizeOrDefault(settings.DataDirectory, storageLayout.DataDirectory);
        settings.RunsDirectory = NormalizeOrDefault(settings.RunsDirectory, storageLayout.RunsDirectory);
        settings.BrowsersDirectory = NormalizeOrDefault(settings.BrowsersDirectory, Path.Combine(settings.DataDirectory, "browsers"));
    }

    private static string NormalizeOrDefault(string path, string fallback)
    {
        var effectivePath = string.IsNullOrWhiteSpace(path) ? fallback : path;
        return Path.GetFullPath(effectivePath);
    }
}

/// <summary>
/// Настройки путей хранения.
/// </summary>
public class AppSettings
{
    public string DataDirectory { get; set; } = string.Empty;
    public string RunsDirectory { get; set; } = string.Empty;
    public string BrowsersDirectory { get; set; } = string.Empty;
    public TelegramSettings Telegram { get; set; } = new();
    public UiLayoutState UiLayout { get; set; } = new();
    public string DatabasePath => Path.Combine(DataDirectory, "webloadtester.db");

    public static AppSettings CreateDefault(AppStorageLayout storageLayout)
    {
        return new AppSettings
        {
            DataDirectory = storageLayout.DataDirectory,
            RunsDirectory = storageLayout.RunsDirectory,
            BrowsersDirectory = storageLayout.BrowsersDirectory,
            Telegram = new TelegramSettings(),
            UiLayout = new UiLayoutState()
        };
    }
}

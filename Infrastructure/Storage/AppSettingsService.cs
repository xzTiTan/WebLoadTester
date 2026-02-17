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

    public AppSettingsService()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebLoadTester");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "settings.json");
        Settings = Load();
        EnsureDirectories(Settings);
    }

    public AppSettings Settings { get; private set; }

    public Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        EnsureDirectories(Settings);
        return File.WriteAllTextAsync(_settingsPath, json);
    }

    private AppSettings Load()
    {
        if (File.Exists(_settingsPath))
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded != null)
            {
                loaded.Telegram ??= new TelegramSettings();
                return loaded;
            }
        }

        return AppSettings.CreateDefault();
    }

    private static void EnsureDirectories(AppSettings settings)
    {
        Directory.CreateDirectory(settings.DataDirectory);
        Directory.CreateDirectory(settings.RunsDirectory);
        Directory.CreateDirectory(settings.BrowsersDirectory);
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
    public string DatabasePath => Path.Combine(DataDirectory, "webloadtester.db");

    public static AppSettings CreateDefault()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebLoadTester");
        var dataDirectory = Path.Combine(root, "data");
        return new AppSettings
        {
            DataDirectory = dataDirectory,
            RunsDirectory = Path.Combine(root, "runs"),
            BrowsersDirectory = Path.Combine(dataDirectory, "browsers"),
            Telegram = new TelegramSettings()
        };
    }
}

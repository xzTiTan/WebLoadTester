using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

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
    }

    public AppSettings Settings { get; private set; }

    public Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
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
                return loaded;
            }
        }

        return AppSettings.CreateDefault();
    }
}

/// <summary>
/// Настройки путей хранения.
/// </summary>
public class AppSettings
{
    public string DataDirectory { get; set; } = string.Empty;
    public string RunsDirectory { get; set; } = string.Empty;
    public string ProfilesDirectory { get; set; } = string.Empty;

    public string DatabasePath => Path.Combine(DataDirectory, "webloadtester.db");

    public static AppSettings CreateDefault()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WebLoadTester");
        return new AppSettings
        {
            DataDirectory = Path.Combine(root, "data"),
            RunsDirectory = Path.Combine(root, "runs"),
            ProfilesDirectory = Path.Combine(root, "profiles")
        };
    }
}

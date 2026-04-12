using System;
using System.IO;

namespace WebLoadTester.Infrastructure.Storage;

public sealed record AppStorageLayout(
    string RootDirectory,
    string SettingsPath,
    string DataDirectory,
    string RunsDirectory,
    string BrowsersDirectory,
    string DatabasePath,
    bool IsPortable);

public static class AppStoragePathResolver
{
    private const string AppDirectoryName = "WebLoadTester";

    public static AppStorageLayout Resolve()
    {
        var portableRoot = NormalizeRoot(AppContext.BaseDirectory);
        if (CanWriteToDirectory(portableRoot))
        {
            return BuildLayout(portableRoot, isPortable: true);
        }

        var appDataRoot = NormalizeRoot(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDirectoryName));
        Directory.CreateDirectory(appDataRoot);
        return BuildLayout(appDataRoot, isPortable: false);
    }

    private static AppStorageLayout BuildLayout(string rootDirectory, bool isPortable)
    {
        var dataDirectory = Path.Combine(rootDirectory, "data");
        var browsersDirectory = Path.Combine(dataDirectory, "browsers");
        return new AppStorageLayout(
            rootDirectory,
            Path.Combine(rootDirectory, "settings.json"),
            dataDirectory,
            Path.Combine(rootDirectory, "runs"),
            browsersDirectory,
            Path.Combine(dataDirectory, "webloadtester.db"),
            isPortable);
    }

    private static bool CanWriteToDirectory(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
            var probePath = Path.Combine(directoryPath, $".wlt_write_probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeRoot(string path) => Path.GetFullPath(path);
}

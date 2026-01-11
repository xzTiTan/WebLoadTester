using Microsoft.Playwright;

namespace WebLoadTester.Infrastructure.Playwright;

public static class PlaywrightFactory
{
    public static string EnsureBrowsersPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "browsers");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", path);
        return path;
    }

    public static async Task<IPlaywright> CreateAsync()
    {
        EnsureBrowsersPath();
        return await Microsoft.Playwright.Playwright.CreateAsync();
    }
}

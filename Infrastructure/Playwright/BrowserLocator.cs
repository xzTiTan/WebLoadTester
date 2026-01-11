namespace WebLoadTester.Infrastructure.Playwright;

public static class BrowserLocator
{
    public static string EnsureBrowsersPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "browsers");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", path);
        return path;
    }
}

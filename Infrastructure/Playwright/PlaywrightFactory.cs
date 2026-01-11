using Microsoft.Playwright;

namespace WebLoadTester.Infrastructure.Playwright;

public sealed class PlaywrightFactory
{
    public async Task<IPlaywright> CreateAsync()
    {
        BrowserLocator.EnsureBrowsersPath();
        return await Microsoft.Playwright.Playwright.CreateAsync();
    }
}

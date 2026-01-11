using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace WebLoadTester.Infrastructure.Playwright;

public static class PlaywrightFactory
{
    public static async Task<IPlaywright> CreateAsync()
    {
        var browsersPath = Path.Combine(AppContext.BaseDirectory, "browsers");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);
        return await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
    }
}

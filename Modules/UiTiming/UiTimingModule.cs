using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Playwright;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.UiTiming;

public sealed class UiTimingModule : ITestModule
{
    public string Id => "ui-timing";
    public string DisplayName => "UI Timing";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiTimingSettings);

    public object CreateDefaultSettings() => new UiTimingSettings();

    public IReadOnlyList<string> Validate(object settings)
    {
        if (settings is not UiTimingSettings s)
        {
            return new[] { "Invalid settings" };
        }

        if (s.Urls.Count == 0)
        {
            return new[] { "Urls list is empty" };
        }

        if (s.RepeatsPerUrl <= 0)
        {
            return new[] { "RepeatsPerUrl must be > 0" };
        }

        if (s.Concurrency <= 0)
        {
            return new[] { "Concurrency must be > 0" };
        }

        return Array.Empty<string>();
    }

    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiTimingSettings)settings;
        var start = ctx.Now;
        var results = new ConcurrentBag<ResultItem>();
        var channel = Channel.CreateUnbounded<string>();
        foreach (var url in s.Urls.SelectMany(url => Enumerable.Repeat(url, s.RepeatsPerUrl)))
        {
            await channel.Writer.WriteAsync(url, ct);
        }
        channel.Writer.Complete();

        var concurrency = Math.Min(s.Concurrency, ctx.Limits.MaxUiConcurrency);
        var waitUntil = s.WaitUntil == UiTimingWaitUntil.Load ? WaitUntilState.Load : WaitUntilState.DOMContentLoaded;
        IPlaywright? playwright = null;
        IBrowser? browser = null;
        try
        {
            playwright = await PlaywrightFactory.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }
        catch (PlaywrightException ex)
        {
            ctx.Log.Log($"Playwright error: {ex.Message}. Install browsers in ./browsers.");
            throw;
        }

        var workers = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
        {
            await foreach (var url in channel.Reader.ReadAllAsync(ct))
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var context = await browser.NewContextAsync();
                    var page = await context.NewPageAsync();
                    await page.GotoAsync(url, new PageGotoOptions { WaitUntil = waitUntil });
                    sw.Stop();
                    results.Add(new RunResult
                    {
                        Kind = "Timing",
                        Name = url,
                        Url = url,
                        Success = true,
                        DurationMs = sw.Elapsed.TotalMilliseconds
                    });
                    await context.CloseAsync();
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results.Add(new RunResult
                    {
                        Kind = "Timing",
                        Name = url,
                        Url = url,
                        Success = false,
                        DurationMs = sw.Elapsed.TotalMilliseconds,
                        ErrorMessage = ex.Message,
                        ErrorType = ex.GetType().Name
                    });
                }
            }
        }, ct)).ToArray();

        await Task.WhenAll(workers);
        if (browser is not null)
        {
            await browser.CloseAsync();
        }
        playwright?.Dispose();

        return new TestReport
        {
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = start,
            FinishedAt = ctx.Now,
            Status = TestStatus.Completed,
            AppVersion = AppVersionProvider.GetVersion(),
            OsDescription = Environment.OSVersion.ToString(),
            SettingsSnapshot = System.Text.Json.JsonSerializer.SerializeToElement(settings),
            Results = results.ToList()
        };
    }
}

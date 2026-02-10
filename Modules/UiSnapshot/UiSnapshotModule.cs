using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.UiSnapshot;

/// <summary>
/// Модуль массового снятия скриншотов UI.
/// </summary>
public class UiSnapshotModule : ITestModule
{
    public string Id => "ui.snapshot";
    public string DisplayName => "UI снимки";
    public string Description => "Создаёт скриншоты страниц для фиксации доступности и визуального состояния.";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiSnapshotSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию.
    /// </summary>
    public object CreateDefaultSettings()
    {
        return new UiSnapshotSettings
        {
            Targets = new List<SnapshotTarget>
            {
                new() { Url = "https://example.com", Tag = "Example" }
            },
            WaitUntil = "load",
            TimeoutSeconds = 30,
            ScreenshotFormat = "png"
        };
    }

    /// <summary>
    /// Проверяет корректность параметров снимков.
    /// </summary>
    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not UiSnapshotSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (s.Targets.Count == 0)
        {
            errors.Add("At least one URL is required");
        }

        if (s.Targets.Any(target => !Uri.TryCreate(target.Url, UriKind.Absolute, out _)))
        {
            errors.Add("Each URL must be absolute");
        }

        if (s.TimeoutSeconds <= 0)
        {
            errors.Add("TimeoutSeconds must be positive");
        }

        if (!string.Equals(s.ScreenshotFormat, "png", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("ScreenshotFormat must be png");
        }

        if ((s.ViewportWidth.HasValue && s.ViewportWidth.Value > 0) ^
            (s.ViewportHeight.HasValue && s.ViewportHeight.Value > 0))
        {
            errors.Add("ViewportWidth and ViewportHeight must be set together");
        }

        return errors;
    }

    /// <summary>
    /// Делает скриншоты указанных URL и формирует результат.
    /// </summary>
    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiSnapshotSettings)settings;
        var result = new ModuleResult();

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            ctx.Log.Error("Playwright browsers not found. Install browsers into ./browsers.");
            result.Status = TestStatus.Failed;
            result.Results.Add(new RunResult("Playwright")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = "Playwright",
                ErrorMessage = "Install browsers"
            });
            return result;
        }

        using var playwright = await PlaywrightFactory.CreateAsync();
        var waitUntil = s.WaitUntil switch
        {
            "domcontentloaded" => WaitUntilState.DOMContentLoaded,
            "networkidle" => WaitUntilState.NetworkIdle,
            _ => WaitUntilState.Load
        };
        var results = new List<ResultBase>();
        var completed = 0;
        var total = s.Targets.Count;
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = ctx.Profile.Headless });

        foreach (var target in s.Targets)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var page = await browser.NewPageAsync(new BrowserNewPageOptions
                {
                    ViewportSize = s.ViewportWidth.HasValue && s.ViewportHeight.HasValue &&
                                   s.ViewportWidth.Value > 0 && s.ViewportHeight.Value > 0
                        ? new ViewportSize { Width = s.ViewportWidth.Value, Height = s.ViewportHeight.Value }
                        : null
                });
                await page.GotoAsync(target.Url, new PageGotoOptions
                {
                    WaitUntil = waitUntil,
                    Timeout = s.TimeoutSeconds * 1000
                });
                var bytes = await page.ScreenshotAsync(new PageScreenshotOptions
                {
                    FullPage = s.FullPage,
                    Type = ScreenshotType.Png
                });
                var fileName = $"snapshot_{Sanitize(target.Url)}.png";
                var path = await ctx.Artifacts.SaveScreenshotAsync(ctx.RunId, fileName, bytes);
                sw.Stop();
                results.Add(new RunResult(target.Tag ?? target.Url)
                {
                    Success = true,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ScreenshotPath = path
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new RunResult(target.Tag ?? target.Url)
                {
                    Success = false,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ErrorType = ex.GetType().Name,
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                var done = Interlocked.Increment(ref completed);
                ctx.Progress.Report(new ProgressUpdate(done, total, "UI снимки"));
            }
        }
        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }

    /// <summary>
    /// Превращает URL в безопасное имя файла.
    /// </summary>
    private static string Sanitize(string url)
    {
        foreach (var ch in System.IO.Path.GetInvalidFileNameChars())
        {
            url = url.Replace(ch, '_');
        }
        return url.Replace("https://", string.Empty).Replace("http://", string.Empty).Replace("/", "_");
    }

}

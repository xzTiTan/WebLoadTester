using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Core.Services;
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.UiSnapshot;

/// <summary>
/// Модуль массового снятия скриншотов UI.
/// </summary>
public class UiSnapshotModule : ITestModule
{
    public string Id => "ui.snapshot";
    public string DisplayName => "UI снимки";
    public string Description => "Снимает скриншоты целевых URL/селекторов и сохраняет артефакты в runs/<RunId>/screenshots.";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiSnapshotSettings);

    public object CreateDefaultSettings()
    {
        return new UiSnapshotSettings
        {
            Targets = new List<SnapshotTarget>
            {
                new() { Url = "https://example.com", Name = "example", Selector = string.Empty }
            },
            WaitUntil = UiWaitUntil.DomContentLoaded,
            TimeoutSeconds = 30,
            ScreenshotFormat = "png",
            FullPage = true
        };
    }

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not UiSnapshotSettings s)
        {
            errors.Add("Некорректный тип настроек UI снимков.");
            return errors;
        }

        NormalizeLegacyTargets(s);

        if (s.Targets.Count == 0)
        {
            errors.Add("Добавьте хотя бы одну цель для снимка.");
        }

        for (var i = 0; i < s.Targets.Count; i++)
        {
            var target = s.Targets[i];
            if (!Uri.TryCreate(target.Url, UriKind.Absolute, out _))
            {
                errors.Add($"Цель {i + 1}: укажите абсолютный URL.");
            }
        }

        if (s.TimeoutSeconds <= 0)
        {
            errors.Add("TimeoutSeconds должен быть больше 0.");
        }

        if (!string.Equals(s.ScreenshotFormat, "png", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("ScreenshotFormat для MVP должен быть png.");
        }

        var hasWidth = s.ViewportWidth.GetValueOrDefault() > 0;
        var hasHeight = s.ViewportHeight.GetValueOrDefault() > 0;
        if (hasWidth ^ hasHeight)
        {
            errors.Add("ViewportWidth и ViewportHeight задаются вместе.");
        }

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            errors.Add($"Chromium не установлен. Нажмите «Установить Chromium» в приложении (или выполните playwright install chromium). Путь: {PlaywrightFactory.GetBrowsersPath()}");
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiSnapshotSettings)settings;
        NormalizeLegacyTargets(s);

        var result = new ModuleResult();

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            var browsersPath = PlaywrightFactory.GetBrowsersPath();
            ctx.Log.Error($"[UiSnapshot] Chromium не найден. Установите браузеры в: {browsersPath}");
            result.Status = TestStatus.Failed;
            result.Results.Add(new RunResult("Playwright")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = "Playwright",
                ErrorMessage = $"Chromium не установлен. Выполните playwright install chromium (path: {browsersPath})."
            });
            return result;
        }

        result.Artifacts.AddRange(await WorkerArtifactPathBuilder.EnsureWorkerProfileSnapshotsAsync(ctx, s, ct));

        using var playwright = await PlaywrightFactory.CreateAsync();
        var runProfileDir = WorkerArtifactPathBuilder.GetWorkerProfilesDir(ctx.RunFolder, ctx.WorkerId);
        Directory.CreateDirectory(runProfileDir);

        await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(
            runProfileDir,
            new BrowserTypeLaunchPersistentContextOptions { Headless = ctx.Profile.Headless });

        var results = new List<ResultBase>();
        var waitUntilState = ToWaitUntilState(s.WaitUntil);
        var timeoutMs = Math.Max(1, s.TimeoutSeconds) * 1000;
        var total = s.Targets.Count;
        var completed = 0;

        for (var i = 0; i < s.Targets.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var target = s.Targets[i];
            var targetIndex = i + 1;
            var sw = Stopwatch.StartNew();
            string? screenshotPath = null;
            var selectorFound = false;

            try
            {
                var page = await browser.NewPageAsync();
                if (s.ViewportWidth.GetValueOrDefault() > 0 && s.ViewportHeight.GetValueOrDefault() > 0)
                {
                    await page.SetViewportSizeAsync(s.ViewportWidth!.Value, s.ViewportHeight!.Value);
                }

                await page.GotoAsync(target.Url, new PageGotoOptions
                {
                    WaitUntil = waitUntilState,
                    Timeout = timeoutMs
                });

                var fileName = BuildSnapshotFileName(targetIndex, target.Name, target.Url);
                var relativePath = BuildScreenshotRelativePath(ctx, fileName);
                if (!string.IsNullOrWhiteSpace(target.Selector))
                {
                    var locator = page.Locator(target.Selector);
                    await locator.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = timeoutMs
                    });

                    var bytes = await locator.ScreenshotAsync(new LocatorScreenshotOptions { Type = ScreenshotType.Png });
                    selectorFound = true;
                    screenshotPath = await ctx.Artifacts.SaveScreenshotAsync(ctx.RunId, relativePath, bytes);
                }
                else
                {
                    var bytes = await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Type = ScreenshotType.Png,
                        FullPage = s.FullPage
                    });

                    screenshotPath = await ctx.Artifacts.SaveScreenshotAsync(ctx.RunId, relativePath, bytes);
                }

                sw.Stop();
                var details = new
                {
                    url = target.Url,
                    hasSelector = !string.IsNullOrWhiteSpace(target.Selector),
                    selectorFound,
                    fullPage = s.FullPage,
                    waitUntil = s.WaitUntil.ToString(),
                    viewport = s.ViewportWidth.GetValueOrDefault() > 0 && s.ViewportHeight.GetValueOrDefault() > 0
                        ? new { width = s.ViewportWidth, height = s.ViewportHeight }
                        : null,
                    bytes = TryReadScreenshotSize(ctx, screenshotPath),
                    elapsedMs = sw.Elapsed.TotalMilliseconds
                };

                var displayName = GetTargetDisplayName(target);
                results.Add(new RunResult($"Target: {displayName}")
                {
                    Success = true,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ScreenshotPath = screenshotPath,
                    DetailsJson = JsonSerializer.Serialize(details)
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                var details = new
                {
                    url = target.Url,
                    hasSelector = !string.IsNullOrWhiteSpace(target.Selector),
                    selectorFound,
                    fullPage = s.FullPage,
                    waitUntil = s.WaitUntil.ToString(),
                    viewport = s.ViewportWidth.GetValueOrDefault() > 0 && s.ViewportHeight.GetValueOrDefault() > 0
                        ? new { width = s.ViewportWidth, height = s.ViewportHeight }
                        : null,
                    elapsedMs = sw.Elapsed.TotalMilliseconds
                };

                ctx.Log.Error($"[UiSnapshot] Цель {targetIndex} failed: {ex.GetType().Name}: {ex.Message}");
                var displayName = GetTargetDisplayName(target);
                results.Add(new RunResult($"Target: {displayName}")
                {
                    Success = false,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ErrorType = ex.GetType().Name,
                    ErrorMessage = ex.Message,
                    DetailsJson = JsonSerializer.Serialize(details)
                });
            }
            finally
            {
                var done = Interlocked.Increment(ref completed);
                ctx.Progress.Report(new ProgressUpdate(done, total, $"UI снимки: {done}/{total}"));
            }
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }

    private static string BuildScreenshotRelativePath(IRunContext ctx, string fileName)
    {
        return WorkerArtifactPathBuilder.GetWorkerScreenshotStoreRelativePath(ctx.WorkerId, ctx.Iteration, fileName);
    }

    private static long? TryReadScreenshotSize(IRunContext ctx, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var path = Path.Combine(ctx.RunFolder, relativePath);
        return File.Exists(path) ? new FileInfo(path).Length : null;
    }

    private static string BuildSnapshotFileName(int index, string? name, string url)
    {
        var source = string.IsNullOrWhiteSpace(name) ? BuildHostPathToken(url) : name;
        var safe = SanitizeFileToken(source);
        return $"snap_{index:00}_{safe}_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.png";
    }

    private static string GetTargetDisplayName(SnapshotTarget target)
    {
        if (!string.IsNullOrWhiteSpace(target.Name))
        {
            return target.Name!;
        }

        if (!Uri.TryCreate(target.Url, UriKind.Absolute, out var uri))
        {
            return target.Url;
        }

        var path = string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/" ? string.Empty : uri.AbsolutePath;
        return $"{uri.Host}{path}";
    }

    private static string BuildHostPathToken(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "target";
        }

        var path = uri.AbsolutePath.Trim('/').Replace('/', '_');
        return string.IsNullOrWhiteSpace(path) ? uri.Host : $"{uri.Host}_{path}";
    }

    private static string SanitizeFileToken(string value)
    {
        var token = Regex.Replace(value, "[^a-zA-Z0-9._-]+", "_");
        token = token.Trim('_');
        if (string.IsNullOrWhiteSpace(token))
        {
            token = "target";
        }

        return token.Length > 64 ? token[..64] : token;
    }

    private static WaitUntilState ToWaitUntilState(UiWaitUntil value)
    {
        return value switch
        {
            UiWaitUntil.DomContentLoaded => WaitUntilState.DOMContentLoaded,
            UiWaitUntil.NetworkIdle => WaitUntilState.NetworkIdle,
            _ => WaitUntilState.Load
        };
    }

    private static void NormalizeLegacyTargets(UiSnapshotSettings settings)
    {
        foreach (var target in settings.Targets)
        {
            if (string.IsNullOrWhiteSpace(target.Name) && !string.IsNullOrWhiteSpace(target.Tag))
            {
                target.Name = target.Tag;
            }
        }
    }
}

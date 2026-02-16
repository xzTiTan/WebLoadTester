using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using WebLoadTester.Core.Contracts;
using WebLoadTester.Core.Domain;
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.UiTiming;

/// <summary>
/// Модуль замеров времени загрузки страниц через Playwright.
/// </summary>
public class UiTimingModule : ITestModule
{
    public string Id => "ui.timing";
    public string DisplayName => "UI тайминги";
    public string Description => "Измеряет время загрузки целевых URL и сохраняет навигационные метрики в DetailsJson.";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiTimingSettings);

    public object CreateDefaultSettings()
    {
        return new UiTimingSettings
        {
            Targets = new List<TimingTarget>
            {
                new() { Url = "https://example.com" }
            },
            WaitUntil = UiWaitUntil.DomContentLoaded,
            TimeoutSeconds = 30
        };
    }

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not UiTimingSettings s)
        {
            errors.Add("Некорректный тип настроек UI таймингов.");
            return errors;
        }

        if (s.Targets.Count == 0)
        {
            errors.Add("Добавьте хотя бы один URL для замеров.");
        }

        for (var i = 0; i < s.Targets.Count; i++)
        {
            if (!Uri.TryCreate(s.Targets[i].Url, UriKind.Absolute, out _))
            {
                errors.Add($"Цель {i + 1}: укажите абсолютный URL.");
            }
        }

        if (s.TimeoutSeconds <= 0)
        {
            errors.Add("TimeoutSeconds должен быть больше 0.");
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiTimingSettings)settings;
        var result = new ModuleResult();

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            var browsersPath = PlaywrightFactory.GetBrowsersPath();
            ctx.Log.Error($"[UiTiming] Chromium не найден. Установите браузеры в: {browsersPath}");
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

        using var playwright = await PlaywrightFactory.CreateAsync();
        var runProfileDir = Path.Combine(ctx.RunFolder, "profile");
        Directory.CreateDirectory(runProfileDir);

        await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(
            runProfileDir,
            new BrowserTypeLaunchPersistentContextOptions { Headless = ctx.Profile.Headless });

        var waitUntilState = ToWaitUntilState(s.WaitUntil);
        var timeoutMs = Math.Max(1, s.TimeoutSeconds) * 1000;
        var results = new List<ResultBase>();
        var total = s.Targets.Count;
        var completed = 0;

        for (var i = 0; i < s.Targets.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var target = s.Targets[i];
            var index = i + 1;
            var sw = Stopwatch.StartNew();

            try
            {
                var page = await browser.NewPageAsync();
                await page.GotoAsync(target.Url, new PageGotoOptions
                {
                    WaitUntil = waitUntilState,
                    Timeout = timeoutMs
                });

                sw.Stop();
                var nav = await ReadNavigationTimingAsync(page);
                var details = JsonSerializer.Serialize(new
                {
                    url = target.Url,
                    waitUntil = s.WaitUntil.ToString(),
                    totalMs = sw.Elapsed.TotalMilliseconds,
                    nav,
                    timestampUtc = DateTimeOffset.UtcNow
                });

                results.Add(new TimingResult($"Timing: {GetDisplayName(target.Url)}")
                {
                    Url = target.Url,
                    Iteration = index,
                    Success = true,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    DetailsJson = details
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                var details = JsonSerializer.Serialize(new
                {
                    url = target.Url,
                    waitUntil = s.WaitUntil.ToString(),
                    totalMs = sw.Elapsed.TotalMilliseconds,
                    timestampUtc = DateTimeOffset.UtcNow
                });

                ctx.Log.Error($"[UiTiming] Цель {index} failed: {ex.GetType().Name}: {ex.Message}");
                results.Add(new TimingResult($"Timing: {GetDisplayName(target.Url)}")
                {
                    Url = target.Url,
                    Iteration = index,
                    Success = false,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ErrorType = ex.GetType().Name,
                    ErrorMessage = ex.Message,
                    DetailsJson = details
                });
            }
            finally
            {
                var done = Interlocked.Increment(ref completed);
                ctx.Progress.Report(new ProgressUpdate(done, total, $"UI тайминги: {done}/{total}"));
            }
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }

    private static async Task<object?> ReadNavigationTimingAsync(IPage page)
    {
        try
        {
            var nav = await page.EvaluateAsync<JsonElement>("""
                () => {
                  const entry = performance.getEntriesByType('navigation')[0];
                  if (!entry) return null;
                  return {
                    domContentLoadedMs: entry.domContentLoadedEventEnd,
                    loadEventMs: entry.loadEventEnd,
                    responseEndMs: entry.responseEnd,
                    requestStartMs: entry.requestStart,
                    transferSize: entry.transferSize ?? null
                  };
                }
            """);

            return nav.ValueKind == JsonValueKind.Null ? null : nav;
        }
        catch
        {
            return null;
        }
    }

    private static string GetDisplayName(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var path = string.IsNullOrWhiteSpace(uri.AbsolutePath) || uri.AbsolutePath == "/" ? string.Empty : uri.AbsolutePath;
        return $"{uri.Host}{path}";
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
}

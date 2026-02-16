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
using WebLoadTester.Infrastructure.Playwright;

namespace WebLoadTester.Modules.UiScenario;

/// <summary>
/// Модуль запуска UI-сценариев через Playwright.
/// </summary>
public class UiScenarioModule : ITestModule
{
    public string Id => "ui.scenario";
    public string DisplayName => "UI сценарий";
    public string Description => "Выполняет UI-сценарии с шагами Action/Selector/Value/Delay, фиксируя результаты и скриншоты.";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiScenarioSettings);

    public object CreateDefaultSettings()
    {
        return new UiScenarioSettings
        {
            Steps = new List<UiStep>
            {
                new() { Action = UiStepAction.Navigate, Value = "https://example.com", DelayMs = 0 },
                new() { Action = UiStepAction.WaitForSelector, Selector = "body", DelayMs = 0 }
            }
        };
    }

    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not UiScenarioSettings scenario)
        {
            errors.Add("Некорректный тип настроек UI сценария.");
            return errors;
        }

        NormalizeLegacySteps(scenario);

        if (scenario.TimeoutMs <= 0)
        {
            errors.Add("Общий таймаут сценария должен быть больше 0 мс.");
        }

        if (scenario.Steps.Count == 0)
        {
            errors.Add("Добавьте хотя бы один шаг сценария.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(scenario.TargetUrl) && scenario.Steps.First().Action != UiStepAction.Navigate)
        {
            errors.Add("Укажите TargetUrl или сделайте первым шагом действие «Переход».");
        }

        for (var i = 0; i < scenario.Steps.Count; i++)
        {
            var step = scenario.Steps[i];
            var index = i + 1;
            var selector = (step.Selector ?? string.Empty).Trim();
            var value = (step.Value ?? string.Empty).Trim();

            switch (step.Action)
            {
                case UiStepAction.Navigate:
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        errors.Add($"Шаг {index}: для действия «Переход» заполните поле Value (URL).");
                    }
                    break;

                case UiStepAction.WaitForSelector:
                case UiStepAction.Click:
                    if (string.IsNullOrWhiteSpace(selector))
                    {
                        errors.Add($"Шаг {index}: для действия «{GetActionRu(step.Action)}» заполните Selector.");
                    }
                    break;

                case UiStepAction.Fill:
                case UiStepAction.AssertText:
                    if (string.IsNullOrWhiteSpace(selector) || string.IsNullOrWhiteSpace(value))
                    {
                        errors.Add($"Шаг {index}: для действия «{GetActionRu(step.Action)}» заполните Selector и Value.");
                    }
                    break;

                case UiStepAction.Delay:
                    if (step.DelayMs <= 0)
                    {
                        errors.Add($"Шаг {index}: для действия «Пауза» DelayMs должен быть больше 0.");
                    }
                    break;

                case UiStepAction.Screenshot:
                    // selector/value опциональны
                    break;
            }

            if (step.DelayMs < 0)
            {
                errors.Add($"Шаг {index}: DelayMs не может быть отрицательным.");
            }
        }

        return errors;
    }

    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var scenario = (UiScenarioSettings)settings;
        NormalizeLegacySteps(scenario);

        var result = new ModuleResult();
        var stepCount = scenario.Steps.Count;
        var profileTimeoutMs = Math.Max(1, ctx.Profile.TimeoutSeconds) * 1000;
        var effectiveTimeoutMs = scenario.TimeoutMs > 0 ? Math.Min(scenario.TimeoutMs, profileTimeoutMs) : profileTimeoutMs;

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            var browsersPath = PlaywrightFactory.GetBrowsersPath();
            ctx.Log.Error($"[UiScenario] Chromium не найден. Установите браузеры в: {browsersPath}");
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

        var results = new List<ResultBase>();
        var totalUnits = stepCount + 1;
        ctx.Progress.Report(new ProgressUpdate(0, totalUnits, "Запуск браузера"));
        ctx.Log.Info($"[UiScenario] Launching browser (Headless={ctx.Profile.Headless})");

        using var playwright = await PlaywrightFactory.CreateAsync();
        var profileDir = Path.Combine(ctx.RunFolder, "profile");
        Directory.CreateDirectory(profileDir);

        await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(profileDir, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = ctx.Profile.Headless
        });

        var page = browser.Pages.FirstOrDefault() ?? await browser.NewPageAsync();
        page.SetDefaultTimeout(effectiveTimeoutMs);
        page.SetDefaultNavigationTimeout(effectiveTimeoutMs);

        if (!string.IsNullOrWhiteSpace(scenario.TargetUrl))
        {
            var target = NormalizeUrl(scenario.TargetUrl);
            ctx.Log.Info($"[UiScenario] TargetUrl: {target}");
            await page.GotoAsync(target, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = effectiveTimeoutMs
            });
            ctx.Progress.Report(new ProgressUpdate(1, totalUnits, "Стартовая страница загружена"));
        }
        else
        {
            ctx.Progress.Report(new ProgressUpdate(1, totalUnits, "Старт без TargetUrl"));
        }

        for (var i = 0; i < stepCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var stepIndex = i + 1;
            var step = scenario.Steps[i];
            var stepName = $"Шаг {stepIndex}";
            var stepActionRu = GetActionRu(step.Action);
            var selector = (step.Selector ?? string.Empty).Trim();
            var value = (step.Value ?? string.Empty).Trim();

            if (step.DelayMs > 0)
            {
                await Task.Delay(step.DelayMs, ct);
            }

            var stopwatch = Stopwatch.StartNew();
            var success = true;
            string? errorType = null;
            string? errorMessage = null;
            string? screenshotPath = null;
            string currentUrl = page.Url;

            try
            {
                ctx.Log.Info($"[UiScenario] {stepName}: {stepActionRu}");

                switch (step.Action)
                {
                    case UiStepAction.Navigate:
                        {
                            var url = NormalizeUrl(value);
                            await page.GotoAsync(url, new PageGotoOptions
                            {
                                WaitUntil = WaitUntilState.DOMContentLoaded,
                                Timeout = effectiveTimeoutMs
                            });
                            currentUrl = page.Url;
                            break;
                        }

                    case UiStepAction.WaitForSelector:
                        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                        {
                            State = WaitForSelectorState.Visible,
                            Timeout = effectiveTimeoutMs
                        });
                        break;

                    case UiStepAction.Click:
                        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                        {
                            State = WaitForSelectorState.Visible,
                            Timeout = effectiveTimeoutMs
                        });
                        await page.ClickAsync(selector, new PageClickOptions { Timeout = effectiveTimeoutMs });
                        await TryWaitForNetworkIdleAsync(page);
                        currentUrl = page.Url;
                        break;

                    case UiStepAction.Fill:
                        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                        {
                            State = WaitForSelectorState.Visible,
                            Timeout = effectiveTimeoutMs
                        });
                        await page.FillAsync(selector, value, new PageFillOptions { Timeout = effectiveTimeoutMs });
                        break;

                    case UiStepAction.AssertText:
                        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                        {
                            State = WaitForSelectorState.Visible,
                            Timeout = effectiveTimeoutMs
                        });
                        var locator = page.Locator(selector);
                        var text = await locator.InnerTextAsync(new LocatorInnerTextOptions { Timeout = effectiveTimeoutMs });
                        if (text?.Contains(value, StringComparison.OrdinalIgnoreCase) != true)
                        {
                            throw new InvalidOperationException($"Ожидался текст «{value}», фактически: «{text}».");
                        }
                        break;

                    case UiStepAction.Screenshot:
                        screenshotPath = await SaveStepScreenshotAsync(ctx, page, selector, stepIndex, step.Action, value, ct);
                        break;

                    case UiStepAction.Delay:
                        await Task.Delay(Math.Max(1, step.DelayMs), ct);
                        break;
                }

                if (step.Action != UiStepAction.Screenshot && ctx.Profile.ScreenshotsPolicy == ScreenshotsPolicy.Always)
                {
                    screenshotPath = await SaveStepScreenshotAsync(ctx, page, selector, stepIndex, step.Action, null, ct);
                }
            }
            catch (Exception ex)
            {
                success = false;
                errorType = ex.GetType().Name;
                errorMessage = ex.Message;
                ctx.Log.Error($"[UiScenario] {stepName} failed: {errorType}: {errorMessage}");

                if (ctx.Profile.ScreenshotsPolicy is ScreenshotsPolicy.OnError or ScreenshotsPolicy.Always)
                {
                    screenshotPath = await SaveStepScreenshotAsync(ctx, page, selector, stepIndex, step.Action, "error", ct);
                }
            }
            finally
            {
                stopwatch.Stop();

                var details = new
                {
                    action = step.Action.ToString(),
                    selector = string.IsNullOrWhiteSpace(selector) ? null : selector,
                    value = string.IsNullOrWhiteSpace(value) ? null : value,
                    delayMs = step.DelayMs,
                    url = currentUrl
                };

                results.Add(new StepResult(stepName)
                {
                    Success = success,
                    DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                    ErrorType = errorType,
                    ErrorMessage = errorMessage,
                    Action = step.Action.ToString(),
                    Selector = string.IsNullOrWhiteSpace(selector) ? null : selector,
                    ScreenshotPath = screenshotPath,
                    DetailsJson = JsonSerializer.Serialize(details)
                });

                ctx.Progress.Report(new ProgressUpdate(stepIndex + 1, totalUnits, $"Шаг {stepIndex}/{stepCount}"));
            }
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }

    private static async Task TryWaitForNetworkIdleAsync(IPage page)
    {
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 1500 });
        }
        catch
        {
            // best-effort: не должен ронять шаг
        }
    }

    private static async Task<string?> SaveStepScreenshotAsync(
        IRunContext ctx,
        IPage page,
        string? selector,
        int stepIndex,
        UiStepAction action,
        string? customName,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var actionName = SanitizeFileName(action.ToString().ToLowerInvariant());
        var suffix = string.IsNullOrWhiteSpace(customName) ? string.Empty : $"_{SanitizeFileName(customName)}";
        var fileName = $"step_{stepIndex:00}_{actionName}_{DateTimeOffset.Now:HHmmssfff}{suffix}.png";

        byte[] bytes;
        if (!string.IsNullOrWhiteSpace(selector) && action == UiStepAction.Screenshot)
        {
            var locator = page.Locator(selector);
            await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 3000 });
            bytes = await locator.ScreenshotAsync();
        }
        else
        {
            bytes = await page.ScreenshotAsync(new PageScreenshotOptions { FullPage = true });
        }

        return await ctx.Artifacts.SaveScreenshotAsync(ctx.RunId, fileName, bytes);
    }

    private static string NormalizeUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"https://{trimmed}";
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "screenshot";
        }

        var normalized = Regex.Replace(value.Trim(), "[^a-zA-Z0-9_\-]+", "_");
        return normalized.Length > 40 ? normalized[..40] : normalized;
    }

    private static string GetActionRu(UiStepAction action)
    {
        return action switch
        {
            UiStepAction.Navigate => "Переход",
            UiStepAction.WaitForSelector => "Ожидание элемента",
            UiStepAction.Click => "Клик",
            UiStepAction.Fill => "Ввод текста",
            UiStepAction.AssertText => "Проверка текста",
            UiStepAction.Screenshot => "Скриншот",
            UiStepAction.Delay => "Пауза",
            _ => action.ToString()
        };
    }

    private static void NormalizeLegacySteps(UiScenarioSettings settings)
    {
        foreach (var step in settings.Steps)
        {
            if (string.IsNullOrWhiteSpace(step.Value) && !string.IsNullOrWhiteSpace(step.Text))
            {
                step.Value = step.Text;
            }

            // миграция старого формата Selector+Text
            if (step.Action == UiStepAction.WaitForSelector)
            {
                var hasSelector = !string.IsNullOrWhiteSpace(step.Selector);
                if (hasSelector)
                {
                    step.Action = string.IsNullOrWhiteSpace(step.Value) ? UiStepAction.Click : UiStepAction.Fill;
                }
            }
        }
    }
}

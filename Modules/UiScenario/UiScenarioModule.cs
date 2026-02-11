using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public string Description => "Выполняет UI-сценарии с кликами и вводом текста, фиксируя шаги и скриншоты.";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiScenarioSettings);

    /// <summary>
    /// Создаёт настройки по умолчанию со стартовым шагом.
    /// </summary>
    public object CreateDefaultSettings()
    {
        return new UiScenarioSettings
        {
            Steps = new List<UiStep>
            {
                new() { Selector = "body", Action = UiStepAction.Click }
            }
        };
    }

    /// <summary>
    /// Проверяет корректность параметров сценария.
    /// </summary>
    public IReadOnlyList<string> Validate(object settings)
    {
        var errors = new List<string>();
        if (settings is not UiScenarioSettings s)
        {
            errors.Add("Invalid settings type");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(s.TargetUrl))
        {
            errors.Add("TargetUrl is required");
        }

        if (s.TimeoutMs <= 0)
        {
            errors.Add("Timeout must be positive");
        }

        if (s.Steps.Count == 0)
        {
            errors.Add("At least one step is required");
        }

        for (var i = 0; i < s.Steps.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(s.Steps[i].Selector))
            {
                errors.Add($"Step {i + 1}: Selector is required");
            }
        }

        return errors;
    }

    /// <summary>
    /// Запускает сценарий в браузере и формирует результат.
    /// </summary>
    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiScenarioSettings)settings;
        var result = new ModuleResult();
        var totalSteps = s.Steps.Count;
        var profileTimeoutMs = Math.Max(1, ctx.Profile.TimeoutSeconds) * 1000;
        var scenarioTimeoutMs = s.TimeoutMs > 0 ? s.TimeoutMs : profileTimeoutMs;
        var effectiveTimeoutMs = Math.Min(scenarioTimeoutMs, profileTimeoutMs);
        var normalizedTargetUrl = NormalizeUrl(s.TargetUrl);

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            var browsersPath = PlaywrightFactory.GetBrowsersPath();
            ctx.Log.Error($"[UiScenario] Chromium browser not found. Install browsers into: {browsersPath}");
            result.Status = TestStatus.Failed;
            result.Results.Add(new RunResult("Playwright")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = "Playwright",
                ErrorMessage = $"Chromium is not installed. Run playwright install chromium (path: {browsersPath})."
            });
            return result;
        }

        var results = new List<ResultBase>();
        ctx.Progress.Report(new ProgressUpdate(0, totalSteps + 2, "Запуск браузера"));
        ctx.Log.Info($"[UiScenario] Launching browser (Headless={ctx.Profile.Headless})...");

        using var playwright = await PlaywrightFactory.CreateAsync();
        var runProfileDir = Path.Combine(ctx.RunFolder, "profile");
        Directory.CreateDirectory(runProfileDir);
        await using var browser = await playwright.Chromium.LaunchPersistentContextAsync(runProfileDir, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = ctx.Profile.Headless
        });
        var page = browser.Pages.FirstOrDefault() ?? await browser.NewPageAsync();
        page.SetDefaultTimeout(effectiveTimeoutMs);
        page.SetDefaultNavigationTimeout(effectiveTimeoutMs);

        ctx.Progress.Report(new ProgressUpdate(1, totalSteps + 2, "Открытие страницы"));
        ctx.Log.Info($"[UiScenario] Navigating to {normalizedTargetUrl}");
        await page.GotoAsync(normalizedTargetUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = effectiveTimeoutMs
        });

        for (var index = 0; index < totalSteps; index++)
        {
            var step = s.Steps[index];
            var isFill = !string.IsNullOrWhiteSpace(step.Text);
            var actionText = isFill ? "Fill" : "Click";
            var sw = Stopwatch.StartNew();
            var success = true;
            string? errorMessage = null;
            string? errorType = null;
            string? screenshotPath = null;
            try
            {
                var timeout = Math.Min(step.TimeoutMs > 0 ? step.TimeoutMs : effectiveTimeoutMs, effectiveTimeoutMs);
                ctx.Log.Info($"[UiScenario] Step {index + 1}: {actionText} {step.Selector}");
                if (isFill)
                {
                    await page.FillAsync(step.Selector, step.Text!, new PageFillOptions
                    {
                        Timeout = timeout
                    });
                }
                else
                {
                    await page.ClickAsync(step.Selector, new PageClickOptions
                    {
                        Timeout = timeout
                    });
                }
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
                errorType = ex.GetType().Name;
                ctx.Log.Error($"[UiScenario] Step {index + 1} failed: {errorType}: {errorMessage}");
            }
            finally
            {
                sw.Stop();
                var policy = ctx.Profile.ScreenshotsPolicy;
                if ((policy == ScreenshotsPolicy.Always) ||
                    (!success && policy == ScreenshotsPolicy.OnError))
                {
                    screenshotPath = await SaveScreenshotAsync(ctx, page, $"step_{index + 1}.png");
                }

                results.Add(new StepResult($"Step {index + 1}")
                {
                    Success = success,
                    DurationMs = sw.Elapsed.TotalMilliseconds,
                    ErrorType = errorType,
                    ErrorMessage = errorMessage,
                    Action = actionText,
                    Selector = step.Selector,
                    ScreenshotPath = screenshotPath
                });

                ctx.Progress.Report(new ProgressUpdate(index + 2, totalSteps + 2, $"Шаг {index + 1}/{totalSteps}"));
            }

            if (!success)
            {
                if (s.ErrorPolicy == StepErrorPolicy.SkipStep)
                {
                    continue;
                }

                if (s.ErrorPolicy == StepErrorPolicy.StopRun)
                {
                    break;
                }

                if (s.ErrorPolicy == StepErrorPolicy.StopAll)
                {
                    throw new RunAbortException("UI scenario stopped due to step failure.");
                }
            }
        }

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
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

    private static async Task<string?> SaveScreenshotAsync(IRunContext ctx, IPage page, string fileName)
    {
        var bytes = await page.ScreenshotAsync();
        return await ctx.Artifacts.SaveScreenshotAsync(ctx.RunId, fileName, bytes);
    }
}

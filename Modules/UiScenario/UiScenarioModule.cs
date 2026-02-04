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

        return errors;
    }

    /// <summary>
    /// Запускает сценарий в браузере и формирует результат.
    /// </summary>
    public async Task<ModuleResult> ExecuteAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiScenarioSettings)settings;
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

        var results = new List<ResultBase>();

        using var playwright = await PlaywrightFactory.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = ctx.Profile.Headless
        });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(s.TargetUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

        var totalSteps = s.Steps.Count;
        for (var index = 0; index < totalSteps; index++)
        {
            var step = s.Steps[index];
            var effectiveAction = step.Action;
            if (effectiveAction == UiStepAction.Fill && string.IsNullOrWhiteSpace(step.Text))
            {
                effectiveAction = UiStepAction.Click;
            }
            var sw = Stopwatch.StartNew();
            var success = true;
            string? errorMessage = null;
            string? errorType = null;
            string? screenshotPath = null;
            try
            {
                var timeout = step.TimeoutMs > 0 ? step.TimeoutMs : s.TimeoutMs;
                switch (effectiveAction)
                {
                    case UiStepAction.Delay:
                        if (step.DelayMs > 0)
                        {
                            await Task.Delay(step.DelayMs, ct);
                        }
                        break;
                    case UiStepAction.WaitForSelector:
                        await page.WaitForSelectorAsync(step.Selector, new PageWaitForSelectorOptions
                        {
                            Timeout = timeout
                        });
                        break;
                    case UiStepAction.Click:
                        await page.ClickAsync(step.Selector, new PageClickOptions
                        {
                            Timeout = timeout
                        });
                        break;
                    case UiStepAction.Fill:
                        if (!string.IsNullOrWhiteSpace(step.Text))
                        {
                            await page.FillAsync(step.Selector, step.Text, new PageFillOptions
                            {
                                Timeout = timeout
                            });
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                success = false;
                errorMessage = ex.Message;
                errorType = ex.GetType().Name;
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
                    Action = effectiveAction.ToString(),
                    Selector = step.Selector,
                    ScreenshotPath = screenshotPath
                });

                ctx.Progress.Report(new ProgressUpdate(index + 1, totalSteps, "UI сценарий"));
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

    private static async Task<string?> SaveScreenshotAsync(IRunContext ctx, IPage page, string fileName)
    {
        var bytes = await page.ScreenshotAsync();
        return await ctx.Artifacts.SaveScreenshotAsync(ctx.RunId, fileName, bytes);
    }
}

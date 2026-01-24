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
using WebLoadTester.Presentation.ViewModels.SettingsViewModels;

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
                new() { Selector = "body", Action = UiStepAction.WaitForSelector }
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

        if (s.TotalRuns <= 0)
        {
            errors.Add("TotalRuns must be positive");
        }

        if (s.Concurrency <= 0)
        {
            errors.Add("Concurrency must be positive");
        }

        if (s.TimeoutMs <= 0)
        {
            errors.Add("Timeout must be positive");
        }

        return errors;
    }

    /// <summary>
    /// Запускает сценарий в браузере и формирует отчёт.
    /// </summary>
    public async Task<TestReport> RunAsync(object settings, IRunContext ctx, CancellationToken ct)
    {
        var s = (UiScenarioSettings)settings;
        var report = new TestReport
        {
            RunId = ctx.RunId,
            TestCaseId = ctx.TestCaseId,
            TestCaseVersion = ctx.TestCaseVersion,
            TestName = ctx.TestName,
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Success,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            ProfileSnapshot = ctx.Profile,
            AppVersion = typeof(UiScenarioModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            ctx.Log.Error("Playwright browsers not found. Install browsers into ./browsers.");
            report.Status = TestStatus.Failed;
            report.Results.Add(new RunResult("Playwright")
            {
                Success = false,
                DurationMs = 0,
                ErrorType = "Playwright",
                ErrorMessage = "Install browsers"
            });
            report.FinishedAt = ctx.Now;
            return report;
        }

        var semaphore = new SemaphoreSlim(Math.Min(s.Concurrency, ctx.Limits.MaxUiConcurrency));
        var results = new List<ResultBase>();
        var completed = 0;

        using var playwright = await PlaywrightFactory.CreateAsync();
        var tasks = Enumerable.Range(1, s.TotalRuns).Select(async index =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var sw = Stopwatch.StartNew();
                string? screenshotPath = null;
                IPage? page = null;
                string? failureMessage = null;
                try
                {
                    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = s.Headless
                    });
                    page = await browser.NewPageAsync();
                    await page.GotoAsync(s.TargetUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

                    foreach (var step in s.Steps)
                    {
                        try
                        {
                            var timeout = step.TimeoutMs > 0 ? step.TimeoutMs : s.TimeoutMs;
                            switch (step.Action)
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
                        catch (Exception) when (s.ErrorPolicy == StepErrorPolicy.SkipStep)
                        {
                            continue;
                        }
                        catch (Exception) when (s.ErrorPolicy == StepErrorPolicy.StopRun)
                        {
                            failureMessage = "Step error: stopped current run";
                            break;
                        }
                    }

                    if (s.ScreenshotMode == ScreenshotMode.Always && page != null)
                    {
                        screenshotPath = await SaveScreenshotAsync(ctx, page, $"run_{index}.png");
                    }

                    sw.Stop();
                    results.Add(new RunResult($"Run {index}")
                    {
                        Success = failureMessage == null,
                        DurationMs = sw.Elapsed.TotalMilliseconds,
                        ScreenshotPath = screenshotPath,
                        ErrorMessage = failureMessage
                    });
                }
                catch (Exception ex)
                {
                    if (s.ScreenshotMode == ScreenshotMode.OnFailure && page != null)
                    {
                        screenshotPath = await SaveScreenshotAsync(ctx, page, $"run_{index}_error.png");
                    }
                    sw.Stop();
                    results.Add(new RunResult($"Run {index}")
                    {
                        Success = false,
                        DurationMs = sw.Elapsed.TotalMilliseconds,
                        ErrorType = ex.GetType().Name,
                        ErrorMessage = ex.Message
                    });
                    if (s.ErrorPolicy == StepErrorPolicy.StopAll)
                    {
                        throw;
                    }
                }
                finally
                {
                    var done = Interlocked.Increment(ref completed);
                    ctx.Progress.Report(new ProgressUpdate(done, s.TotalRuns, "UI сценарий"));
                }
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);

        report.Results = results;
        report.FinishedAt = ctx.Now;
        return report;
    }

    private static async Task<string?> SaveScreenshotAsync(IRunContext ctx, IPage page, string fileName)
    {
        var bytes = await page.ScreenshotAsync();
        return await ctx.Artifacts.SaveScreenshotAsync(bytes, ctx.RunFolder, fileName);
    }
}

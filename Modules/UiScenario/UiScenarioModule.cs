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
    public string DisplayName => "UI Scenario";
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
                new() { Selector = "body", Action = UiStepAction.WaitForVisible }
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
            ModuleId = Id,
            ModuleName = DisplayName,
            Family = Family,
            StartedAt = ctx.Now,
            Status = TestStatus.Completed,
            SettingsSnapshot = System.Text.Json.JsonSerializer.Serialize(s),
            AppVersion = typeof(UiScenarioModule).Assembly.GetName().Version?.ToString() ?? string.Empty,
            OsDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            ctx.Log.Error("Playwright browsers not found. Install browsers into ./browsers.");
            report.Status = TestStatus.Error;
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
                try
                {
                    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = s.Headless
                    });
                    var page = await browser.NewPageAsync();
                    await page.GotoAsync(s.TargetUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load });

                    foreach (var step in s.Steps)
                    {
                        await page.WaitForSelectorAsync(step.Selector, new PageWaitForSelectorOptions
                        {
                            Timeout = step.TimeoutMs
                        });
                        if (step.Action == UiStepAction.Click)
                        {
                            await page.ClickAsync(step.Selector);
                        }
                        else if (step.Action == UiStepAction.FillText && !string.IsNullOrWhiteSpace(step.Text))
                        {
                            await page.FillAsync(step.Selector, step.Text);
                        }
                    }

                    if (s.ScreenshotAfterScenario)
                    {
                        var bytes = await page.ScreenshotAsync();
                        var fileName = $"run_{index}.png";
                        var runFolder = ctx.Artifacts.CreateRunFolder(report.StartedAt.ToString("yyyyMMdd_HHmmss"));
                        screenshotPath = await ctx.Artifacts.SaveScreenshotAsync(bytes, runFolder, fileName);
                    }

                    sw.Stop();
                    results.Add(new RunResult($"Run {index}")
                    {
                        Success = true,
                        DurationMs = sw.Elapsed.TotalMilliseconds,
                        ScreenshotPath = screenshotPath
                    });
                }
                catch (Exception ex)
                {
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
                    ctx.Progress.Report(new ProgressUpdate(done, s.TotalRuns, "UI Scenario"));
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
}

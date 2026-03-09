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

namespace WebLoadTester.Modules.UiScenario;

/// <summary>
/// Модуль запуска UI-сценариев через Playwright.
/// </summary>
public class UiScenarioModule : ITestModule
{
    public string Id => "ui.scenario";
    public string DisplayName => "Регрессионное тестирование";
    public string Description => "Повторно выполняет сценарий шагов (Action/Selector/Value/DelayMs) для регрессионной проверки сайта.";
    public TestFamily Family => TestFamily.UiTesting;
    public Type SettingsType => typeof(UiScenarioSettings);

    public object CreateDefaultSettings()
    {
        return new UiScenarioSettings
        {
            Steps = new List<UiStep>
            {
                new() { Action = UiStepAction.Navigate, Value = "https://www.google.com/", DelayMs = 0 },
                new() { Action = UiStepAction.WaitForSelector, Selector = "input[name=q]", DelayMs = 0 },
                new() { Action = UiStepAction.Fill, Selector = "input[name=q]", Value = "test", DelayMs = 0 },
                new() { Action = UiStepAction.Click, Selector = "body > div.L3eUgb > div.o3j99.ikrT4e.om7nvf > form > div:nth-child(1) > div.A8SBwf > div.FPdoLc.lJ9FBc > center > input.RNmpXc", DelayMs = 0 },
                new() { Action = UiStepAction.WaitForSelector, Selector = "#search", DelayMs = 0 }
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

        if (!PlaywrightFactory.HasBrowsersInstalled())
        {
            errors.Add($"Chromium не установлен. Нажмите «Установить Chromium» в приложении (или выполните playwright install chromium). Путь: {PlaywrightFactory.GetBrowsersPath()}");
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

        result.Artifacts.AddRange(await WorkerArtifactPathBuilder.EnsureWorkerProfileSnapshotsAsync(ctx, scenario, ct));

        using var playwright = await PlaywrightFactory.CreateAsync();
        var profileDir = WorkerArtifactPathBuilder.GetWorkerProfilesDir(ctx.RunFolder, ctx.WorkerId);
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
                        try
                        {
                            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
                            {
                                State = WaitForSelectorState.Visible,
                                Timeout = effectiveTimeoutMs
                            });
                            await page.ClickAsync(selector, new PageClickOptions { Timeout = effectiveTimeoutMs });
                        }
                        catch
                        {
                            var fallbackClicked = false;
                            if (await page.Locator("input[name=btnK]").CountAsync() > 0)
                            {
                                await page.ClickAsync("input[name=btnK]", new PageClickOptions { Timeout = effectiveTimeoutMs });
                                fallbackClicked = true;
                            }

                            if (!fallbackClicked)
                            {
                                await page.PressAsync("input[name=q]", "Enter", new PagePressOptions { Timeout = effectiveTimeoutMs });
                            }
                        }

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

        if (ctx.Profile.ScreenshotsPolicy == ScreenshotsPolicy.Always)
        {
            try
            {
                var finalScreenshot = await SaveStepScreenshotAsync(ctx, page, null, stepCount + 1, UiStepAction.Screenshot, "final", ct);
                results.Add(new RunResult("Scenario: итоговый скриншот")
                {
                    Success = true,
                    DurationMs = 0,
                    ScreenshotPath = finalScreenshot,
                    DetailsJson = JsonSerializer.Serialize(new { stage = "iteration-final", policy = "Always" })
                });
            }
            catch (Exception ex)
            {
                ctx.Log.Warn($"[UiScenario] Не удалось сохранить итоговый скриншот: {ex.Message}");
            }
        }

        AppendRegressionComparisonResult(result, scenario, ctx, results);

        result.Results = results;
        result.Status = results.Any(r => !r.Success) ? TestStatus.Failed : TestStatus.Success;
        return result;
    }

    private static void AppendRegressionComparisonResult(ModuleResult result, UiScenarioSettings scenario, IRunContext ctx, List<ResultBase> currentResults)
    {
        var comparison = BuildRegressionComparison(scenario, ctx, currentResults);
        var comparisonStatus = comparison.HasBaseline
            ? comparison.NewErrors > 0
                ? TestStatus.Failed
                : TestStatus.Success
            : TestStatus.Skipped;

        var message = comparison.HasBaseline
            ? $"Базовый прогон: {comparison.BaselineRunId}. Изменено шагов: {comparison.ChangedSteps}, новые ошибки: {comparison.NewErrors}, исправлено ошибок: {comparison.ResolvedErrors}."
            : "Базовый прогон не найден: сравнение не выполнено.";

        currentResults.Add(new RunResult("Регрессионное сравнение")
        {
            Success = comparisonStatus != TestStatus.Failed,
            ErrorType = comparisonStatus == TestStatus.Failed ? "RegressionDiff" : null,
            ErrorMessage = comparisonStatus == TestStatus.Failed ? message : null,
            DurationMs = 0,
            DetailsJson = JsonSerializer.Serialize(new
            {
                hasBaseline = comparison.HasBaseline,
                baselineRunId = comparison.BaselineRunId,
                baselineMissingReason = comparison.BaselineMissingReason,
                changedSteps = comparison.ChangedSteps,
                newErrors = comparison.NewErrors,
                resolvedErrors = comparison.ResolvedErrors,
                comparedSteps = comparison.ComparedSteps,
                message
            })
        });

        if (!comparison.HasBaseline)
        {
            ctx.Log.Info("[UiScenario] Регрессионное сравнение: базовый успешный прогон не найден.");
            return;
        }

        ctx.Log.Info($"[UiScenario] Регрессионное сравнение выполнено. Baseline={comparison.BaselineRunId}, changed={comparison.ChangedSteps}, newErrors={comparison.NewErrors}, resolved={comparison.ResolvedErrors}.");
    }

    private static RegressionComparison BuildRegressionComparison(UiScenarioSettings scenario, IRunContext ctx, List<ResultBase> currentResults)
    {
        var fingerprint = BuildScenarioFingerprint(scenario);
        var baseline = TryFindBaselineReport(ctx, fingerprint);
        if (baseline == null)
        {
            return new RegressionComparison(false, null, "Нет успешного прогона с такой конфигурацией.", 0, 0, 0, 0);
        }

        var currentSteps = currentResults.OfType<StepResult>().ToList();
        var compared = Math.Min(currentSteps.Count, baseline.StepSnapshots.Count);
        var changed = 0;
        var newErrors = 0;
        var resolvedErrors = 0;
        for (var i = 0; i < compared; i++)
        {
            var current = currentSteps[i];
            var prev = baseline.StepSnapshots[i];
            if (!string.Equals(current.Action, prev.Action, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(current.Selector ?? string.Empty, prev.Selector ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(current.Success.ToString(), prev.Success.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                changed++;
            }

            if (!prev.Success && current.Success)
            {
                resolvedErrors++;
            }

            if (prev.Success && !current.Success)
            {
                newErrors++;
            }
        }

        changed += Math.Abs(currentSteps.Count - baseline.StepSnapshots.Count);
        return new RegressionComparison(true, baseline.RunId, null, changed, newErrors, resolvedErrors, compared);
    }

    private static BaselineScenarioSnapshot? TryFindBaselineReport(IRunContext ctx, string fingerprint)
    {
        try
        {
            var runsRoot = ctx.Artifacts.RunsRoot;
            if (string.IsNullOrWhiteSpace(runsRoot) || !Directory.Exists(runsRoot))
            {
                return null;
            }

            var candidates = new List<BaselineScenarioSnapshot>();
            foreach (var runDir in Directory.EnumerateDirectories(runsRoot))
            {
                var runId = Path.GetFileName(runDir);
                if (string.Equals(runId, ctx.RunId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var reportPath = Path.Combine(runDir, "report.json");
                if (!File.Exists(reportPath))
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
                var root = doc.RootElement;
                if (!root.TryGetProperty("moduleId", out var moduleId) || !string.Equals(moduleId.GetString(), "ui.scenario", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!root.TryGetProperty("status", out var status) || !string.Equals(status.GetString(), "Success", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!root.TryGetProperty("moduleSettings", out var moduleSettings))
                {
                    continue;
                }

                if (!string.Equals(BuildScenarioFingerprint(moduleSettings), fingerprint, StringComparison.Ordinal))
                {
                    continue;
                }

                var finishedAt = DateTimeOffset.MinValue;
                if (root.TryGetProperty("finishedAtUtc", out var finishedElement) && finishedElement.ValueKind == JsonValueKind.String)
                {
                    DateTimeOffset.TryParse(finishedElement.GetString(), out finishedAt);
                }

                var steps = new List<BaselineStepSnapshot>();
                if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (!item.TryGetProperty("kind", out var kind) || !string.Equals(kind.GetString(), "Step", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var action = string.Empty;
                        var selector = string.Empty;
                        if (item.TryGetProperty("extra", out var extra) && extra.ValueKind == JsonValueKind.Object)
                        {
                            if (extra.TryGetProperty("action", out var actionEl) && actionEl.ValueKind == JsonValueKind.String)
                            {
                                action = actionEl.GetString() ?? string.Empty;
                            }

                            if (extra.TryGetProperty("selector", out var selectorEl) && selectorEl.ValueKind == JsonValueKind.String)
                            {
                                selector = selectorEl.GetString() ?? string.Empty;
                            }
                        }

                        var ok = item.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
                        steps.Add(new BaselineStepSnapshot(action, selector, ok));
                    }
                }

                candidates.Add(new BaselineScenarioSnapshot(runId, finishedAt, steps));
            }

            return candidates
                .OrderByDescending(c => c.FinishedAt)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string BuildScenarioFingerprint(UiScenarioSettings settings)
        => BuildScenarioFingerprint(JsonSerializer.SerializeToElement(settings));

    private static string BuildScenarioFingerprint(JsonElement settingsElement)
    {
        var targetUrl = settingsElement.TryGetProperty("targetUrl", out var target) ? target.GetString() ?? string.Empty : string.Empty;
        var timeoutMs = settingsElement.TryGetProperty("timeoutMs", out var timeout) && timeout.TryGetInt32(out var value) ? value : 0;

        var steps = new List<string>();
        if (settingsElement.TryGetProperty("steps", out var stepsEl) && stepsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var step in stepsEl.EnumerateArray())
            {
                var action = step.TryGetProperty("action", out var a) ? a.ToString() : string.Empty;
                var selector = step.TryGetProperty("selector", out var s) ? s.GetString() ?? string.Empty : string.Empty;
                var val = step.TryGetProperty("value", out var v) ? v.GetString() ?? string.Empty : string.Empty;
                var delay = step.TryGetProperty("delayMs", out var d) ? d.ToString() : "0";
                steps.Add($"{action}|{selector}|{val}|{delay}");
            }
        }

        return $"{NormalizeUrlForFingerprint(targetUrl)}||{timeoutMs}||{string.Join("||", steps)}";
    }

    private static string NormalizeUrlForFingerprint(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        return url.Trim().ToLowerInvariant();
    }

    private sealed record BaselineScenarioSnapshot(string RunId, DateTimeOffset FinishedAt, IReadOnlyList<BaselineStepSnapshot> StepSnapshots);
    private sealed record BaselineStepSnapshot(string Action, string Selector, bool Success);
    private sealed record RegressionComparison(bool HasBaseline, string? BaselineRunId, string? BaselineMissingReason, int ChangedSteps, int NewErrors, int ResolvedErrors, int ComparedSteps);

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

        var relative = WorkerArtifactPathBuilder.GetWorkerScreenshotStoreRelativePath(ctx.WorkerId, ctx.Iteration, fileName);
        return await ctx.Artifacts.SaveScreenshotAsync(ctx.RunId, relative, bytes);
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

        var normalized = Regex.Replace(value.Trim(), @"[^a-zA-Z0-9_-]+", "_");
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

            // legacy-поля нормализуем только по данным, не меняя семантику явно заданного Action.
        }
    }
}

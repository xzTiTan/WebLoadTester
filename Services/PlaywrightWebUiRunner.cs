using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using WebLoadTester.Domain;

namespace WebLoadTester.Services
{
    public class PlaywrightWebUiRunner : IWebUiRunner
    {
        public async Task<RunResult> RunOnceAsync(RunRequest request, CancellationToken ct)
        {
            var result = new RunResult
            {
                WorkerId = request.WorkerId,
                RunId = request.RunId,
                StartedAt = DateTime.UtcNow
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _ = PlaywrightBootstrap.EnsureBrowsersPathAndReturn(AppContext.BaseDirectory);
            IPlaywright? playwright = null;
            IBrowser? browser = null;
            IBrowserContext? context = null;
            IPage? page = null;

            try
            {
                try
                {
                    playwright = await Playwright.CreateAsync();
                }
                catch (PlaywrightException ex)
                {
                    var message =
                        $"Playwright browsers not found. Run: powershell -ExecutionPolicy Bypass -File ./playwright.ps1 install chromium. Details: {ex.Message}";
                    request.Logger.Log(message);
                    result.ErrorMessage = message;
                    result.Success = false;
                    return result;
                }

                try
                {
                    browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = request.Settings.Headless
                    });
                }
                catch (PlaywrightException ex)
                {
                    var message =
                        $"Не удалось запустить Chromium. Убедитесь, что браузеры установлены в ./browsers (playwright install chromium). Details: {ex.Message}";
                    request.Logger.Log(message);
                    result.ErrorMessage = message;
                    result.Success = false;
                    return result;
                }

                context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true
                });

                page = await context.NewPageAsync();
                page.SetDefaultTimeout(request.Settings.TimeoutSeconds * 1000);

                ct.ThrowIfCancellationRequested();
                request.Logger.Log($"[W{request.WorkerId}][Run {request.RunId}] Открываю {request.Settings.TargetUrl}");
                await page.GotoAsync(request.Settings.TargetUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded
                });

                var steps = request.Scenario.Steps;
                if (steps.Count == 0)
                {
                    request.Logger.Log($"[W{request.WorkerId}][Run {request.RunId}] Шаги отсутствуют, только проверка загрузки");
                }

                foreach (var step in steps)
                {
                    ct.ThrowIfCancellationRequested();
                    var stepTimer = System.Diagnostics.Stopwatch.StartNew();
                    var stepResult = new StepResult { Selector = step.Selector };
                    try
                    {
                        request.Logger.Log($"[W{request.WorkerId}][Run {request.RunId}] Ожидание {step.Selector}");
                        var locator = page.Locator(step.Selector);
                        await locator.WaitForAsync(new LocatorWaitForOptions
                        {
                            State = WaitForSelectorState.Visible,
                            Timeout = request.Settings.TimeoutSeconds * 1000
                        });

                        request.Logger.Log($"[W{request.WorkerId}][Run {request.RunId}] Клик {step.Selector}");
                        await locator.ClickAsync(new LocatorClickOptions
                        {
                            Timeout = request.Settings.TimeoutSeconds * 1000
                        });

                        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
                        {
                            Timeout = request.Settings.TimeoutSeconds * 1000
                        });

                        stepResult.Success = true;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        stepResult.Success = false;
                        stepResult.ErrorMessage = ex.Message;
                        if (request.Settings.StepErrorPolicy == StepErrorPolicy.SkipStep)
                        {
                            request.Logger.Log($"[W{request.WorkerId}][Run {request.RunId}] Ошибка шага (skip): {ex.Message}");
                        }
                        else if (request.Settings.StepErrorPolicy == StepErrorPolicy.StopRun)
                        {
                            request.Logger.Log($"[W{request.WorkerId}][Run {request.RunId}] Ошибка шага, завершаю прогон: {ex.Message}");
                            result.Steps.Add(stepResult);
                            result.Success = false;
                            result.ErrorMessage = ex.Message;
                            break;
                        }
                        else
                        {
                            request.Logger.Log($"[W{request.WorkerId}][Run {request.RunId}] Ошибка шага, остановка всего теста: {ex.Message}");
                            result.Steps.Add(stepResult);
                            result.Success = false;
                            result.StopAllRequested = true;
                            result.ErrorMessage = ex.Message;
                            request.CancelAll?.Cancel();
                            break;
                        }
                    }
                    finally
                    {
                        stepResult.Duration = stepTimer.Elapsed;
                        result.Steps.Add(stepResult);
                    }
                }

                result.Success = result.StopAllRequested == false && result.Steps.TrueForAll(s => s.Success || request.Settings.StepErrorPolicy == StepErrorPolicy.SkipStep);
                result.FinalUrl = page.Url;

                if (request.Settings.ScreenshotAfterRun)
                {
                    try
                    {
                        var root = request.Settings.ScreenshotDirectory ?? Path.Combine("screenshots", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                        Directory.CreateDirectory(root);
                        var file = Path.Combine(root, $"run_{request.RunId:0000}_w{request.WorkerId}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                        await page.ScreenshotAsync(new PageScreenshotOptions { Path = file, FullPage = true });
                        result.ScreenshotPath = file;
                    }
                    catch (Exception ex)
                    {
                        request.Logger.Log($"[W{request.WorkerId}][Run {request.RunId}] Ошибка скриншота: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                try
                {
                    await page?.CloseAsync();
                }
                catch
                {
                }

                try
                {
                    await context?.CloseAsync();
                }
                catch
                {
                }

                try
                {
                    await browser?.CloseAsync();
                }
                catch
                {
                    request.Logger.Log("Не удалось закрыть браузер корректно");
                }

                playwright?.Dispose();
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.FinishedAt = DateTime.UtcNow;
            }

            return result;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}

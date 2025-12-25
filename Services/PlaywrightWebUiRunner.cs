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
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private IBrowser? _browser;
        private IPlaywright? _playwright;
        private BrowserTypeLaunchOptions _launchOptions = new();

        public async Task InitializeAsync(bool headless)
        {
            if (_browser != null && _launchOptions.Headless == headless)
            {
                return;
            }

            await _initLock.WaitAsync();
            try
            {
                if (_browser != null)
                {
                    await _browser.CloseAsync();
                    _browser = null;
                }

                _launchOptions = new BrowserTypeLaunchOptions
                {
                    Headless = headless
                };

                var browsersPath = Path.Combine(AppContext.BaseDirectory, "browsers");
                Directory.CreateDirectory(browsersPath);
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);

                try
                {
                    _playwright ??= await Playwright.CreateAsync();
                    _browser = await _playwright.Chromium.LaunchAsync(_launchOptions);
                }
                catch (PlaywrightException ex)
                {
                    var message =
                        "Не удалось запустить Chromium. Убедитесь, что браузеры установлены в ./browsers (playwright install chromium). " +
                        ex.Message;
                    throw new InvalidOperationException(message, ex);
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<RunResult> RunOnceAsync(Scenario scenario, RunSettings settings, int workerId, int runId, ILogSink log, CancellationToken ct, CancellationTokenSource? cancelAll = null)
        {
            await InitializeAsync(settings.Headless);

            var result = new RunResult
            {
                WorkerId = workerId,
                RunId = runId,
                StartedAt = DateTime.UtcNow
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            await using var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true
            });

            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(settings.TimeoutSeconds * 1000);

            try
            {
                log.Log($"[W{workerId}][Run {runId}] Открываю {settings.TargetUrl}");
                await page.GotoAsync(settings.TargetUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

                var stepIndex = 0;
                foreach (var step in scenario.Steps)
                {
                    stepIndex++;
                    var stepTimer = System.Diagnostics.Stopwatch.StartNew();
                    var stepResult = new StepResult { Selector = step.Selector };
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        log.Log($"[W{workerId}][Run {runId}] Ожидание {step.Selector}");
                        var locator = page.Locator(step.Selector);
                        await locator.WaitForAsync(new LocatorWaitForOptions
                        {
                            State = WaitForSelectorState.Visible,
                            Timeout = settings.TimeoutSeconds * 1000
                        });

                        log.Log($"[W{workerId}][Run {runId}] Клик {step.Selector}");
                        await locator.ClickAsync();
                        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
                        {
                            Timeout = settings.TimeoutSeconds * 1000
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

                        if (settings.ErrorPolicy == StepErrorPolicy.SkipStep)
                        {
                            log.Log($"[W{workerId}][Run {runId}] Ошибка шага (skip): {ex.Message}");
                            result.Steps.Add(stepResult);
                            continue;
                        }

                        if (settings.ErrorPolicy == StepErrorPolicy.StopRun)
                        {
                            log.Log($"[W{workerId}][Run {runId}] Ошибка шага, завершаю прогон: {ex.Message}");
                            result.Steps.Add(stepResult);
                            throw;
                        }

                        log.Log($"[W{workerId}][Run {runId}] Ошибка шага, остановка всего теста: {ex.Message}");
                        result.Steps.Add(stepResult);
                        cancelAll?.Cancel();
                        throw new OperationCanceledException(ex.Message, ex, ct);
                    }
                    finally
                    {
                        stepResult.Duration = stepTimer.Elapsed;
                        result.Steps.Add(stepResult);
                    }
                }

                result.Success = true;
                result.FinalUrl = page.Url;
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
                if (settings.ScreenshotAfterRun)
                {
                    try
                    {
                        var screenshotRoot = settings.ScreenshotDirectory ?? "screenshots";
                        Directory.CreateDirectory(screenshotRoot);
                        var status = result.Success ? "ok" : "fail";
                        var file = Path.Combine(screenshotRoot, $"run_{runId:0000}_w{workerId}_{DateTime.Now:yyyyMMdd_HHmmss}_{status}.png");
                        await page.ScreenshotAsync(new PageScreenshotOptions { Path = file, FullPage = true });
                        result.ScreenshotPath = file;
                    }
                    catch (Exception ex)
                    {
                        log.Log($"[W{workerId}][Run {runId}] Ошибка скриншота: {ex.Message}");
                    }
                }

                try
                {
                    await page.CloseAsync();
                }
                catch
                {
                    // ignore
                }

                await context.CloseAsync();
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
                result.FinishedAt = DateTime.UtcNow;
            }

            return result;
        }

        public async ValueTask DisposeAsync()
        {
            if (_browser != null)
            {
                await _browser.CloseAsync();
            }

            _browser = null;
            _playwright?.Dispose();
            _playwright = null;
        }
    }
}

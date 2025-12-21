using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Playwright;

namespace WebLoadTester.Views;

public partial class MainWindow : Window
{
    // ===== UI collections =====
    private readonly ObservableCollection<string> _selectors = new();
    private readonly ObservableCollection<string> _log = new();

    // ===== Logging via Channel =====
    private readonly Channel<string> _logChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = true
    });

    private CancellationTokenSource? _runCts;
    private CancellationTokenSource? _logCts;

    public MainWindow()
    {
        InitializeComponent();

        // Bind ItemsSource (перекрывает демо-элементы из XAML)
        SelectorsList.ItemsSource = _selectors;
        LogList.ItemsSource = _log;

        // Defaults for MVP
        UrlTextBox.Text = "https://www.google.com";
        NameTextBox.Text = Environment.MachineName;

        TotalRunsTextBox.Text = "1";
        ConcurrencyTextBox.Text = "1";
        TimeoutTextBox.Text = "15";

        HeadlessCheckBox.IsChecked = true;
        ScreenshotCheckBox.IsChecked = true;

        // Put your requested selector as first step (если список пустой)
        if (_selectors.Count == 0)
        {
            _selectors.Add("body > div.L3eUgb > div.o3j99.ikrT4e.om7nvf > form > div:nth-child(1) > div.A8SBwf > div.FPdoLc.lJ9FBc > center > input.RNmpXc");
        }

        // Wire buttons
        AddSelectorBtn.Click += AddSelectorBtn_Click;
        RemoveSelectorBtn.Click += RemoveSelectorBtn_Click;
        UpSelectorBtn.Click += UpSelectorBtn_Click;
        DownSelectorBtn.Click += DownSelectorBtn_Click;

        StartBtn.Click += StartBtn_Click;
        RestartBtn.Click += RestartBtn_Click;
        StopBtn.Click += StopBtn_Click;

        LoadScenarioBtn.Click += (_, __) => EnqueueLog("LOAD scenario.json — позже");
        SaveScenarioBtn.Click += (_, __) => EnqueueLog("SAVE scenario.json — позже");

        // Start log reader loop
        _logCts = new CancellationTokenSource();
        _ = Task.Run(() => LogReaderLoopAsync(_logCts.Token));

        SetStatus("Idle", "0/0");
        EnqueueLog("Приложение запущено");
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        try { _runCts?.Cancel(); } catch { /* ignore */ }
        try { _logCts?.Cancel(); } catch { /* ignore */ }

        _runCts?.Dispose();
        _logCts?.Dispose();
    }

    // ===========================
    // UI Events
    // ===========================

    private void AddSelectorBtn_Click(object? sender, RoutedEventArgs e)
    {
        _selectors.Add("css_selector_here");
        SelectorsList.SelectedIndex = _selectors.Count - 1;
        EnqueueLog("Добавлен шаг: css_selector_here");
    }

    private void RemoveSelectorBtn_Click(object? sender, RoutedEventArgs e)
    {
        var i = SelectorsList.SelectedIndex;
        if (i < 0 || i >= _selectors.Count) return;

        var removed = _selectors[i];
        _selectors.RemoveAt(i);
        EnqueueLog($"Удалён шаг: {removed}");
    }

    private void UpSelectorBtn_Click(object? sender, RoutedEventArgs e)
    {
        var i = SelectorsList.SelectedIndex;
        if (i <= 0 || i >= _selectors.Count) return;

        (_selectors[i - 1], _selectors[i]) = (_selectors[i], _selectors[i - 1]);
        SelectorsList.SelectedIndex = i - 1;
        EnqueueLog("Шаг перемещён вверх");
    }

    private void DownSelectorBtn_Click(object? sender, RoutedEventArgs e)
    {
        var i = SelectorsList.SelectedIndex;
        if (i < 0 || i >= _selectors.Count - 1) return;

        (_selectors[i + 1], _selectors[i]) = (_selectors[i], _selectors[i + 1]);
        SelectorsList.SelectedIndex = i + 1;
        EnqueueLog("Шаг перемещён вниз");
    }

    private async void StartBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_runCts != null)
        {
            EnqueueLog("Уже запущено");
            return;
        }

        _runCts = new CancellationTokenSource();

        try
        {
            ToggleUi(isRunning: true);

            var url = (UrlTextBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                EnqueueLog("URL пустой — запуск отменён");
                return;
            }

            var selectors = _selectors.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (selectors.Count == 0)
            {
                EnqueueLog("Список селекторов пустой — запуск отменён");
                return;
            }

            var totalRuns = ParseIntOrDefault(TotalRunsTextBox.Text, 1, 1, 1_000_000);
            var concurrency = ParseIntOrDefault(ConcurrencyTextBox.Text, 1, 1, 50);
            var timeoutSec = ParseIntOrDefault(TimeoutTextBox.Text, 15, 1, 600);
            var headless = HeadlessCheckBox.IsChecked ?? true;
            var screenshot = ScreenshotCheckBox.IsChecked ?? false;

            SetStatus("Running", $"0/{totalRuns}");
            EnqueueLog($"Старт: url={url}, totalRuns={totalRuns}, concurrency={concurrency}, timeout={timeoutSec}s, headless={headless}, screenshot={screenshot}");

            await RunWithPlaywrightAsync(
                url: url,
                selectors: selectors,
                totalRuns: totalRuns,
                concurrency: concurrency,
                timeoutSeconds: timeoutSec,
                headless: headless,
                screenshotAfterRun: screenshot,
                token: _runCts.Token);

            SetStatus("Done", $"{totalRuns}/{totalRuns}");
            EnqueueLog("Готово");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Stopped", "—");
            EnqueueLog("Остановлено пользователем");
        }
        catch (Exception ex)
        {
            SetStatus("Error", "—");
            EnqueueLog($"Ошибка: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _runCts?.Dispose();
            _runCts = null;
            ToggleUi(isRunning: false);
        }
    }

    private void StopBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (_runCts == null) return;

        EnqueueLog("Stop: запрос отмены");
        SetStatus("Stopping", "…");
        _runCts.Cancel();
    }

    private void RestartBtn_Click(object? sender, RoutedEventArgs e)
    {
        // Минимальный “restart”: если работает — остановить и попросить стартнуть заново
        if (_runCts != null)
        {
            StopBtn_Click(sender, e);
            EnqueueLog("Restart: остановка запущена (нажми Начать после остановки)");
            return;
        }

        // если не работает — просто старт
        StartBtn_Click(sender, e);
    }

    // ===========================
    // Playwright runner (TPL)
    // ===========================

    private async Task RunWithPlaywrightAsync(
        string url,
        List<string> selectors,
        int totalRuns,
        int concurrency,
        int timeoutSeconds,
        bool headless,
        bool screenshotAfterRun,
        CancellationToken token)
    {
        // Глобальные счётчики прогонов/успехов/ошибок
        int runCounter = 0;
        int ok = 0;
        int fail = 0;

        // Поднимем один браузер, а контексты/страницы — на воркерах
        EnqueueLog("Playwright: CreateAsync()");
        using var playwright = await Playwright.CreateAsync();

        EnqueueLog("Playwright: Launch Chromium");
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = headless
        });

        // Воркеры
        var tasks = new List<Task>(concurrency);
        for (int workerId = 1; workerId <= concurrency; workerId++)
        {
            var id = workerId;
            tasks.Add(Task.Run(async () =>
            {
                // отдельный контекст на воркер
                await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
                {
                    IgnoreHTTPSErrors = true
                });

                while (true)
                {
                    token.ThrowIfCancellationRequested();

                    var current = Interlocked.Increment(ref runCounter);
                    if (current > totalRuns) break;

                    try
                    {
                        EnqueueLog($"[W{id}] Run {current}/{totalRuns}: open page");
                        var page = await context.NewPageAsync();
                        page.SetDefaultTimeout(timeoutSeconds * 1000);

                        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                        await TryDismissGoogleConsentAsync(page);

                        // выполнить шаги
                        foreach (var sel in selectors)
                        {
                            token.ThrowIfCancellationRequested();

                            EnqueueLog($"[W{id}] wait: {Short(sel)}");
                            var loc = page.Locator(sel);
                            await loc.WaitForAsync(new LocatorWaitForOptions
                            {
                                State = WaitForSelectorState.Visible,
                                Timeout = timeoutSeconds * 1000
                            });

                            EnqueueLog($"[W{id}] click: {Short(sel)}");
                            await loc.ClickAsync();
                            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
                            {
                                Timeout = timeoutSeconds * 1000
                            });
                        }

                        EnqueueLog($"[W{id}] done, url={page.Url}");

                        if (screenshotAfterRun)
                        {
                            Directory.CreateDirectory("screenshots");
                            var file = Path.Combine("screenshots", $"run_{current:0000}_w{id}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                            await page.ScreenshotAsync(new PageScreenshotOptions { Path = file, FullPage = true });
                            EnqueueLog($"[W{id}] screenshot: {file}");
                        }

                        await page.CloseAsync();

                        Interlocked.Increment(ref ok);
                        UpdateProgress(ok + fail, totalRuns, ok, fail);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref fail);
                        EnqueueLog($"[W{id}] FAIL run {current}: {ex.GetType().Name}: {ex.Message}");
                        UpdateProgress(ok + fail, totalRuns, ok, fail);
                    }
                }

                EnqueueLog($"[W{id}] worker finished");
            }, token));
        }

        await Task.WhenAll(tasks);
        EnqueueLog($"Итог: OK={ok}, FAIL={fail}, TOTAL={totalRuns}");
    }

    private async Task TryDismissGoogleConsentAsync(IPage page)
    {
        // Небольшая “помощь” для Google consent-баннера.
        // Если его нет — просто тихо выходим.
        string[] candidates =
        {
            "button:has-text(\"Accept all\")",
            "button:has-text(\"I agree\")",
            "button:has-text(\"Принять все\")",
            "button:has-text(\"Согласен\")",
            "button:has-text(\"Принять\")"
        };

        foreach (var c in candidates)
        {
            try
            {
                var loc = page.Locator(c);
                if (await loc.CountAsync() > 0)
                {
                    await loc.First.ClickAsync(new LocatorClickOptions { Timeout = 2000 });
                    EnqueueLog("Google consent закрыт");
                    break;
                }
            }
            catch
            {
                // ignore
            }
        }
    }

    // ===========================
    // Helpers: logging + status
    // ===========================

    private void EnqueueLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logChannel.Writer.TryWrite(line);
    }

    private async Task LogReaderLoopAsync(CancellationToken token)
    {
        try
        {
            while (await _logChannel.Reader.WaitToReadAsync(token))
            {
                while (_logChannel.Reader.TryRead(out var line))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _log.Add(line);

                        // Ограничим размер лога (чтобы UI не распухал)
                        if (_log.Count > 3000)
                            _log.RemoveAt(0);

                        // Автоскролл вниз
                        if (_log.Count > 0)
                            LogList.ScrollIntoView(_log[^1]);
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
    }

    private void SetStatus(string status, string progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = $"Status: {status}";
            ProgressText.Text = $"Progress: {progress}";
        });
    }

    private void UpdateProgress(int done, int total, int ok, int fail)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressText.Text = $"Progress: {done}/{total}   OK: {ok}  FAIL: {fail}";
        });
    }

    private void ToggleUi(bool isRunning)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StartBtn.IsEnabled = !isRunning;
            RestartBtn.IsEnabled = !isRunning; // на MVP пусть будет доступен только когда не идет
            StopBtn.IsEnabled = isRunning;

            LoadScenarioBtn.IsEnabled = !isRunning;
            SaveScenarioBtn.IsEnabled = !isRunning;

            AddSelectorBtn.IsEnabled = !isRunning;
            RemoveSelectorBtn.IsEnabled = !isRunning;
            UpSelectorBtn.IsEnabled = !isRunning;
            DownSelectorBtn.IsEnabled = !isRunning;
        });
    }

    private static int ParseIntOrDefault(string? s, int @default, int min, int max)
    {
        if (!int.TryParse((s ?? "").Trim(), out var v))
            v = @default;

        if (v < min) v = min;
        if (v > max) v = max;
        return v;
    }

    private static string Short(string selector)
    {
        selector = selector.Trim();
        if (selector.Length <= 70) return selector;
        return selector.Substring(0, 67) + "...";
    }
}

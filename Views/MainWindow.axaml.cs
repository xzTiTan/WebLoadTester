using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Microsoft.Playwright;

namespace WebLoadTester.Views;

public partial class MainWindow : Window
{
    private enum StepErrorPolicy
    {
        SkipStep,
        StopRun,
        StopAll
    }

    private string ConfigureLocalPlaywrightBrowsersPath()
    {
        // Папка рядом с exe
        var browsersPath = Path.Combine(AppContext.BaseDirectory, "browsers");

        // Создаём папку (если её нет) — это не скачивает браузер, просто готовит место
        Directory.CreateDirectory(browsersPath);

        // Говорим Playwright искать браузеры ТОЛЬКО здесь
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", browsersPath);

        return browsersPath;
    }

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
        ErrorPolicyComboBox.SelectedIndex = 0;

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

        LoadScenarioBtn.Click += (_, __) => EnqueueLog("Загрузка scenario.json — позже");
        SaveScenarioBtn.Click += (_, __) => EnqueueLog("Сохранение scenario.json — позже");

        // Start log reader loop
        _logCts = new CancellationTokenSource();
        _ = Task.Run(() => LogReaderLoopAsync(_logCts.Token));

        SetStatus("Ожидание", "0/0");
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

    private async void SelectorsList_OnDoubleTapped(object? sender, RoutedEventArgs e)
    {
        await EditSelectedSelectorAsync();
    }

    private async void SelectorsList_OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await EditSelectedSelectorAsync();
        }
    }

    private async Task EditSelectedSelectorAsync()
    {
        var i = SelectorsList.SelectedIndex;
        if (i < 0 || i >= _selectors.Count) return;

        var current = _selectors[i];
        var dlg = new EditSelectorWindow(current);
        var result = await dlg.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(result))
        {
            EnqueueLog("Редактирование: пустая строка или отмена — без изменений.");
            return;
        }

        _selectors[i] = result;
        EnqueueLog($"Обновлён шаг: {Short(result)}");
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
            var errorPolicy = GetSelectedPolicy();

            SetStatus("Выполняется", $"0/{totalRuns}");
            EnqueueLog($"Старт: url={url}, totalRuns={totalRuns}, concurrency={concurrency}, timeout={timeoutSec}s, headless={headless}, screenshot={screenshot}, policy={PolicyToString(errorPolicy)}");

            await RunWithPlaywrightAsync(
                url: url,
                selectors: selectors,
                totalRuns: totalRuns,
                concurrency: concurrency,
                timeoutSeconds: timeoutSec,
                headless: headless,
                screenshotAfterRun: screenshot,
                errorPolicy: errorPolicy,
                token: _runCts.Token);

            SetStatus("Готово", $"{totalRuns}/{totalRuns}");
            EnqueueLog("Готово");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Остановлено", "—");
            EnqueueLog("Остановлено пользователем");
        }
        catch (Exception ex)
        {
            SetStatus("Ошибка", "—");
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

        EnqueueLog("Стоп: запрос отмены");
        SetStatus("Останавливаю", "…");
        _runCts.Cancel();
    }

    private void RestartBtn_Click(object? sender, RoutedEventArgs e)
    {
        // Минимальный “restart”: если работает — остановить и попросить стартнуть заново
        if (_runCts != null)
        {
            StopBtn_Click(sender, e);
            EnqueueLog("Рестарт: остановка запущена (нажми «Начать» после остановки)");
            return;
        }

        // если не работает — просто старт
        StartBtn_Click(sender, e);
    }

    // ===========================
    // Playwright runner (TPL)
    // ===========================

    private StepErrorPolicy GetSelectedPolicy()
    {
        return ErrorPolicyComboBox.SelectedIndex switch
        {
            0 => StepErrorPolicy.SkipStep,
            1 => StepErrorPolicy.StopRun,
            2 => StepErrorPolicy.StopAll,
            _ => StepErrorPolicy.SkipStep
        };
    }

    private static string PolicyToString(StepErrorPolicy policy) => policy switch
    {
        StepErrorPolicy.SkipStep => "Пропустить шаг и продолжить",
        StepErrorPolicy.StopRun => "Остановить текущий прогон (FAIL) и перейти к следующему прогону",
        StepErrorPolicy.StopAll => "Остановить весь тест",
        _ => policy.ToString()
    };

    private async Task RunWithPlaywrightAsync(
        string url,
        List<string> selectors,
        int totalRuns,
        int concurrency,
        int timeoutSeconds,
        bool headless,
        bool screenshotAfterRun,
        StepErrorPolicy errorPolicy,
        CancellationToken token)
    {
        // Глобальные счётчики прогонов/успехов/ошибок
        int runCounter = 0;
        int ok = 0;
        int fail = 0;

        // Поднимем один браузер, а контексты/страницы — на воркерах
        var browsersPath = ConfigureLocalPlaywrightBrowsersPath();
        EnqueueLog($"Каталог браузеров Playwright: {browsersPath}");

        EnqueueLog("Playwright: инициализация");
        using var playwright = await Playwright.CreateAsync();


        EnqueueLog("Playwright: запуск Chromium");
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

                    var runLabel = $"[W{id}] Прогон {current}/{totalRuns}";
                    try
                    {
                        EnqueueLog($"{runLabel}: открываю страницу");
                        var page = await context.NewPageAsync();
                        page.SetDefaultTimeout(timeoutSeconds * 1000);

                        bool runFailed = false;
                        string? failReason = null;
                        string? finalUrl = null;

                        try
                        {
                            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
                            await TryDismissGoogleConsentAsync(page);

                            // выполнить шаги
                            foreach (var sel in selectors)
                            {
                                token.ThrowIfCancellationRequested();

                                try
                                {
                                    EnqueueLog($"{runLabel}: Ожидаю селектор {Short(sel)}");
                                    var loc = page.Locator(sel);
                                    await loc.WaitForAsync(new LocatorWaitForOptions
                                    {
                                        State = WaitForSelectorState.Visible,
                                        Timeout = timeoutSeconds * 1000
                                    });

                                    EnqueueLog($"{runLabel}: Кликаю {Short(sel)}");
                                    await loc.ClickAsync();
                                    await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
                                    {
                                        Timeout = timeoutSeconds * 1000
                                    });
                                    EnqueueLog($"{runLabel}: Шаг выполнен {Short(sel)}");
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    runFailed = true;
                                    failReason = $"{ex.GetType().Name}: {ex.Message}";

                                    if (errorPolicy == StepErrorPolicy.SkipStep)
                                    {
                                        EnqueueLog($"{runLabel}: Ошибка шага {Short(sel)} (пропускаю). Детали: {failReason}");
                                        continue;
                                    }

                                    if (errorPolicy == StepErrorPolicy.StopRun)
                                    {
                                        EnqueueLog($"{runLabel}: Ошибка шага {Short(sel)}. Останавливаю прогон и перехожу к следующему.");
                                        break;
                                    }

                                    EnqueueLog($"{runLabel}: Ошибка шага {Short(sel)}. Останавливаю весь тест.");
                                    _runCts?.Cancel();
                                    throw new OperationCanceledException(token);
                                }
                            }

                            finalUrl = page.Url;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            runFailed = true;
                            failReason ??= $"{ex.GetType().Name}: {ex.Message}";
                            EnqueueLog($"{runLabel}: Ошибка прогона: {failReason}");
                        }
                        finally
                        {
                            if (screenshotAfterRun)
                            {
                                try
                                {
                                    Directory.CreateDirectory("screenshots");
                                    var status = runFailed ? "fail" : "ok";
                                    var file = Path.Combine("screenshots", $"run_{current:0000}_w{id}_{DateTime.Now:yyyyMMdd_HHmmss}_{status}.png");
                                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = file, FullPage = true });
                                    EnqueueLog($"{runLabel}: Скриншот: {file}");
                                }
                                catch (Exception ex)
                                {
                                    EnqueueLog($"{runLabel}: Ошибка при скриншоте: {ex.Message}");
                                }
                            }

                            try
                            {
                                await page.CloseAsync();
                            }
                            catch (Exception ex)
                            {
                                EnqueueLog($"{runLabel}: Ошибка закрытия страницы: {ex.Message}");
                            }
                        }

                        if (runFailed)
                        {
                            Interlocked.Increment(ref fail);
                            UpdateProgress(ok + fail, totalRuns, ok, fail);
                            EnqueueLog($"{runLabel}: ошибка. {failReason ?? "Неизвестная ошибка"}");
                        }
                        else
                        {
                            EnqueueLog($"{runLabel}: Готово, url={finalUrl}");
                            Interlocked.Increment(ref ok);
                            UpdateProgress(ok + fail, totalRuns, ok, fail);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref fail);
                        EnqueueLog($"{runLabel}: ошибка выполнения: {ex.GetType().Name}: {ex.Message}");
                        UpdateProgress(ok + fail, totalRuns, ok, fail);
                    }
                }

                EnqueueLog($"[W{id}] воркер завершён");
            }, token));
        }

        await Task.WhenAll(tasks);
        EnqueueLog($"Итог: ОК={ok}, Ошибок={fail}, Всего={totalRuns}");
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
            StatusText.Text = $"Статус: {status}";
            ProgressText.Text = $"Прогресс: {progress}";
        });
    }

    private void UpdateProgress(int done, int total, int ok, int fail)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressText.Text = $"Прогресс: {done}/{total}   ОК: {ok}  Ошибок: {fail}";
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
            SelectorsList.IsEnabled = !isRunning;
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

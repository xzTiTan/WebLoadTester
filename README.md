# WebLoadTester

**Версия:** v1.1.23 22.03.2026

> **English TL;DR (кратко):** Desktop UI/HTTP/Network test runner on .NET + Avalonia + Playwright. Provides 10 built-in modules (UI сценарии/скриншоты/тайминги, HTTP проверки/нагрузка/ассеты, сетевые и security проверки). Generates JSON + HTML reports and supports Telegram notifications (text). All details below are sourced from the repository.

- [AGENTS.md](AGENTS.md)
- [docs/ (00–08)](docs/)

## 1. Что это и для чего
**WebLoadTester** — настольное приложение для запуска UI/HTTP/сетевых проверок и получения отчётов с метриками (перцентили, ошибки, топ медленных) на основе .NET + Avalonia + Playwright. Реализованные сценарии — это **10 модулей** с собственными настройками и результатами. В UI доступны вкладки: **UI тестирование**, **HTTP тестирование**, **Сеть и безопасность**, **Прогоны** (история запусков). Точка входа: `Program.cs`, UI: `MainWindow.axaml`.

## 2. Для кого (персоны)
- **QA-инженеры** — быстрые UI/HTTP/сетевые прогоны с фиксируемыми отчётами и метриками.
- **DevOps/SRE** — проверка доступности, TCP/TLS/DNS диагностика, базовые security-проверки.
- **Сисадмины** — ручной инструмент для сетевых проверок узлов и TLS-сертификатов.
- **ИТ-отдел/поддержка** — оперативные «preflight» проверки перед релизом.
- **Разработчики** — единый UI для проверок HTTP/ассетов/сценариев и поиска регрессий.

## 3. Ключевые возможности (строго по коду)
- 10 модулей тестирования (UI/HTTP/Network/Security) с настраиваемыми параметрами и валидацией. (`Modules/*`)
- Генерация **JSON** (всегда) и **HTML** (опционально) отчётов с метриками. (`Core/Services/ReportWriters/*`)
- Скриншоты UI в модулях, использующих Playwright. (`Modules/UiScenario`, `Modules/UiSnapshot`)
- Ограничения нагрузки (конкурентность и RPS) через `Limits`. (`Core/Domain/Limits.cs`)
- Telegram-уведомления о старте/прогрессе/финише/ошибке (текстовые сообщения). (`Infrastructure/Telegram/*`)
- SQLite-хранилище тестов, профилей и истории прогонов. (`Infrastructure/Storage/SqliteRunStore.cs`)
- UI с вкладками модулей и вкладкой истории **Прогоны**. (`Presentation/Views/*`)

## 4. Демонстрационный сценарий (5–10 шагов)
1. Клонируйте репозиторий.
2. Проверьте, что установлен **.NET SDK 8.0** (см. `WebLoadTester.csproj`).
3. Соберите проект: `dotnet build`.
4. Установите браузеры Playwright в локальную папку `./browsers` рядом с бинарниками (см. команды ниже).
5. Запустите приложение: `dotnet run`.
6. В UI откройте вкладку **UI тестирование** или **HTTP тестирование**, выберите модуль и заполните настройки.
7. Нажмите **Старт** — лог начнёт заполняться, статус и прогресс обновятся.
8. Перейдите на вкладку **Прогоны** и откройте JSON/HTML отчёт конкретного RunId.
9. При необходимости настройте Telegram через окно **Настройки** и повторите запуск — будут отправлены уведомления.

## 5. Технологии и требования
- **.NET**: `net8.0` (см. `WebLoadTester.csproj`).
- **Avalonia**: 11.3.10 (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`).
- **Playwright**: 1.57.0 (`Microsoft.Playwright`).
- **SQLite**: `Microsoft.Data.Sqlite`.
- **Telegram**: в проекте есть `Telegram.Bot` 22.7.6, но интеграция реализована через собственный HTTP-клиент (`Infrastructure/Telegram/TelegramNotifier.cs`).
- **ОС**: приложение — Avalonia Desktop; явные платформенные ограничения в коде не зафиксированы.

## 6. Быстрый старт (с нуля)
```bash
git clone <repo>
cd WebLoadTester

dotnet restore
dotnet build
dotnet run
```
**Playwright-браузеры:**
- Приложение ожидает браузеры в папке `./browsers` **рядом с бинарником** (`AppContext.BaseDirectory`). (`Infrastructure/Playwright/PlaywrightFactory.cs`)
- Команды установки (после `dotnet build`):
  - **Windows (PowerShell):**
    ```powershell
    $env:PLAYWRIGHT_BROWSERS_PATH = (Join-Path (Get-Location) "bin/Debug/net8.0/browsers")
    .\bin\Debug\net8.0\playwright.ps1 install
    ```
  - **Linux/macOS (bash):**
    ```bash
    PLAYWRIGHT_BROWSERS_PATH=bin/Debug/net8.0/browsers \
      ./bin/Debug/net8.0/playwright.sh install
    ```
  > Для Release/publish замените путь на соответствующий каталог сборки.

**Артефакты и логи:**
- JSON/HTML отчёты: `runs/{RunId}/report.json` (всегда) и `runs/{RunId}/report.html` (опционально).
- Скриншоты и логи: `runs/{RunId}/screenshots/` и `runs/{RunId}/logs/`.
- Лог отображается в UI (канал `LogBus`) и сохраняется в `runs/{RunId}/logs/run.log`.

## 7. Архитектура (высокоуровнево)
**Слои и зависимости:**
- **Presentation** (Avalonia UI, ViewModels/Views) → использует **Core** и **Modules**.
- **Modules** (10 модулей тестирования) → используют **Core** и **Infrastructure**.
- **Core** (контракты, доменные модели, оркестратор, метрики) — основа.
- **Infrastructure** (Playwright/HTTP/Network/Storage/Telegram) — внешние интеграции.

**Основной pipeline выполнения теста:**
1. `MainWindowViewModel.StartAsync` создаёт `RunContext` и `TestOrchestrator`.
2. `TestOrchestrator.RunAsync` валидирует настройки модуля.
3. `ITestModule.RunAsync` выполняет тест, собирает `ResultBase`-результаты.
4. `MetricsCalculator` считает метрики (avg/min/max/p50/p95/p99 и т.д.).
5. `JsonReportWriter` + `HtmlReportWriter` сохраняют отчёты через `ArtifactStore`.
6. UI обновляет прогресс через `ProgressBus` и лог через `LogBus`.

**Start/Stop и CancellationToken:**
- `StartAsync` создаёт `CancellationTokenSource` и передаёт токен в модуль.
- `Stop` вызывает `Cancel()`; модули уважают `CancellationToken` в циклах/ожиданиях.

**Лог в UI:**
- `LogBus` пишет строки в `Channel<string>`.
- `ReadLogAsync` читает канал и добавляет строки в `LogEntries` через Dispatcher UI.

**Формирование TestReport:**
- Базовый отчёт формирует `TestOrchestrator` (метаданные, OS, версия приложения, снимок настроек).
- Модуль заполняет `Results`, `FinishedAt` и статус.

## 8. Структура репозитория
```
/Assets
/Core
  /Contracts        # ITestModule, IRunContext, IArtifactStore, ILogSink...
  /Domain           # TestReport, ResultBase, Limits, Enums...
  /Services         # TestOrchestrator, ModuleRegistry, LogBus, ProgressBus, Metrics...
/Infrastructure
  /Http             # HttpClientProvider
  /Network          # DNS/TCP/TLS probes
  /Playwright       # PlaywrightFactory (browsers path)
  /Storage          # ArtifactStore (runs/profiles)
  /Telegram         # TelegramPolicy, TelegramNotifier
/Modules
  /Availability     # net.availability
  /HttpAssets        # http.assets
  /HttpFunctional    # http.functional
  /HttpPerformance   # http.performance
  /NetDiagnostics    # net.diagnostics
  /Preflight         # net.preflight
  /SecurityBaseline  # net.security
  /UiScenario        # ui.scenario
  /UiSnapshot        # ui.snapshot
  /UiTiming          # ui.timing
/Presentation
  /Common            # AutoScrollBehavior
  /ViewModels        # MainWindow, Settings, Tabs
  /Views             # MainWindow, Tabs, SettingsViews
App.axaml            # темы + DataTemplates
Program.cs           # entry point
WebLoadTester.csproj # зависимости/TargetFramework
```

## 9. МОДУЛИ (10 штук) — подробности
Ниже перечислены все модули, их Id (как в коде), настройки и поведение.

### 9.1 UI сценарий
- **Id:** `ui.scenario` (`Modules/UiScenario/UiScenarioModule.cs`)
- **Цель:** запуск последовательных UI-шагов через Playwright.
- **Настройки (`UiScenarioSettings`):**
  - `TargetUrl`, `TotalRuns`, `Concurrency`, `Headless`, `ErrorPolicy` (`StepErrorPolicy`), `Steps`, `TimeoutMs`, `ScreenshotMode`.
  - `Steps`: `Selector`, `Action` (`WaitForSelector/Click/Fill/Delay`), `Text`, `TimeoutMs`, `DelayMs`.
- **Pipeline:**
  1. Проверка параметров.
  2. Проверка наличия браузеров (иначе ошибка).
  3. Параллельные прогоны с ограничением `Min(Concurrency, Limits.MaxUiConcurrency)`.
  4. Для каждого прогона: `Goto`, выполнение шагов, опционально скриншот.
- **Артефакты:** скриншоты `run_<index>.png` в `runs/{RunId}/screenshots/`.
- **Метрики:** по `RunResult` (успех, duration, ошибка). p50/p95/p99 и TopSlow рассчитываются автоматически.
- **Типичные кейсы:** регрессия UI, smoke-сценарии; полезно QA/разработчикам.

### 9.2 UI снимки
- **Id:** `ui.snapshot` (`Modules/UiSnapshot/UiSnapshotModule.cs`)
- **Цель:** массовое снятие скриншотов списка URL.
- **Настройки (`UiSnapshotSettings`):** `Targets` (URL + Tag), `Concurrency`, `RepeatsPerUrl`, `WaitUntil` (`load`/`domcontentloaded`/`networkidle`), `ExtraDelayMs`, `FullPage`.
- **Pipeline:**
  1. Проверка списка URL.
  2. Playwright → параллельные заходы на каждый URL.
  3. Скриншот каждой страницы, имя `snapshot_<sanitized_url>_<iteration>.png`.
- **Артефакты:** скриншоты в `runs/{RunId}/screenshots/`.
- **Метрики:** `RunResult` на URL.
- **Кейсы:** визуальные сравнения, быстрые проверки доступности UI.

### 9.3 UI тайминги
- **Id:** `ui.timing` (`Modules/UiTiming/UiTimingModule.cs`)
- **Цель:** измерение времени загрузки страниц.
- **Настройки (`UiTimingSettings`):** `Targets` (URL + Tag), `RepeatsPerUrl`, `Concurrency`, `WaitUntil` (`load`/`domcontentloaded`/`networkidle`), `Headless`, `TimeoutMs`.
- **Pipeline:**
  1. Генерация пар (URL × итерации).
  2. Параллельные заходы и замеры времени `page.Goto`.
- **Артефакты:** скриншоты не создаются.
- **Метрики:** `TimingResult` (iteration, url, duration).
- **Кейсы:** контроль производительности UI, SLA по загрузке.

### 9.4 HTTP функциональные проверки
- **Id:** `http.functional` (`Modules/HttpFunctional/HttpFunctionalModule.cs`)
- **Цель:** точечные проверки HTTP-эндпоинтов с ассерциями.
- **Настройки (`HttpFunctionalSettings`/`HttpEndpoint`):**
  - `BaseUrl`, `TimeoutSeconds`.
  - `Endpoints`: `Name`, `Path`, `Method`, `Headers`, `Body`, `StatusCodeEquals`, `MaxLatencyMs`, `HeaderContainsKey`, `HeaderContainsValue`, `BodyContains`.
- **Pipeline:** последовательный обход эндпоинтов, запросы `HttpClient`.
- **Артефакты:** нет.
- **Метрики:** `CheckResult` (status code, latency, asserts).
- **Кейсы:** API health checks, базовые регрессии контрактов.

### 9.5 HTTP производительность
- **Id:** `http.performance` (`Modules/HttpPerformance/HttpPerformanceModule.cs`)
- **Цель:** нагрузочный HTTP-тест с конкурентностью и RPS-лимитом.
- **Настройки (`HttpPerformanceSettings`):** `Url`, `Method`, `TotalRequests`, `Concurrency`, `RpsLimit`, `TimeoutSeconds`.
- **Pipeline:**
  1. Параллельные запросы с семафором `Min(Concurrency, Limits.MaxHttpConcurrency)`.
  2. При `RpsLimit` ограничение до `Min(RpsLimit, Limits.MaxRps)`.
- **Артефакты:** нет.
- **Метрики:** `CheckResult` на каждый запрос; перцентили и TopSlow.
- **Кейсы:** оценка throughput/latency, тестирование API на нагрузку.

### 9.6 HTTP ассеты
- **Id:** `http.assets` (`Modules/HttpAssets/HttpAssetsModule.cs`)
- **Цель:** проверка ассетов по размеру, типу и времени ответа.
- **Настройки (`HttpAssetsSettings`/`AssetItem`):** `BaseUrl`, `Assets` (`Path`, `ExpectedContentType`, `MaxSizeBytes`, `MaxLatencyMs`), `TimeoutSeconds`.
- **Pipeline:** запрос каждого ассета, валидация размера/Content-Type/латентности.
- **Артефакты:** нет.
- **Метрики:** `CheckResult` с ошибками типа `Asset`.
- **Кейсы:** мониторинг CDN, проверка статических ресурсов.

### 9.7 Сетевая диагностика
- **Id:** `net.diagnostics` (`Modules/NetDiagnostics/NetDiagnosticsModule.cs`)
- **Цель:** DNS/TCP/TLS проверки для хоста.
- **Настройки (`NetDiagnosticsSettings`):** `Hostname`, `Ports`, `AutoPortsByScheme`, `EnableDns`, `EnableTcp`, `EnableTls`.
- **Pipeline:** сетевые пробы (`NetworkProbes`) + фиксация деталей.
- **Артефакты:** нет.
- **Метрики:** `ProbeResult` (details, duration).
- **Кейсы:** диагностика инфраструктуры, поиск сетевых проблем.

### 9.8 Доступность
- **Id:** `net.availability` (`Modules/Availability/AvailabilityModule.cs`)
- **Цель:** периодические HTTP/TCP проверки доступности.
- **Настройки (`AvailabilitySettings`):** `Target`, `TargetType` (`Http`/`Tcp`), `IntervalSeconds`, `DurationMinutes`, `TimeoutMs`, `FailThreshold`.
- **Pipeline:** цикл проверок по интервалу; фиксация последовательных падений.
- **Артефакты:** нет.
- **Метрики:** `ProbeResult` с `Details = "Downtime window"` при превышении порога.
- **Ограничения:** `IntervalSeconds >= 5` проверяется валидацией; лимиты `Limits.MinAvailabilityIntervalSeconds` и `Limits.MaxAvailabilityDurationMinutes` доступны в `Limits`, но в модуле не применяются.
- **Кейсы:** мониторинг стабильности сервиса.

### 9.9 Базовая безопасность
- **Id:** `net.security` (`Modules/SecurityBaseline/SecurityBaselineModule.cs`)
- **Цель:** проверка базовых security-практик (headers/redirect/TLS).
- **Настройки (`SecurityBaselineSettings`):** `Url`, `CheckHeaders`, `CheckRedirectHttpToHttps`, `CheckTlsExpiry`.
- **Pipeline:**
  - проверка заголовков `Strict-Transport-Security`, `X-Frame-Options`, `X-Content-Type-Options`, `Content-Security-Policy`;
  - проверка редиректа HTTP→HTTPS;
  - TLS-проверка срока сертификата.
- **Артефакты:** нет.
- **Метрики:** `CheckResult` и `ProbeResult`.
- **Кейсы:** безопасность публичных веб-приложений.

### 9.10 Предварительные проверки
- **Id:** `net.preflight` (`Modules/Preflight/PreflightModule.cs`)
- **Цель:** быстрый preflight-комплект DNS/TCP/TLS/HTTP для целевого URL.
- **Настройки (`PreflightSettings`):** `Target`, `CheckDns`, `CheckTcp`, `CheckTls`, `CheckHttp`.
- **Pipeline:** компоновка проверок по флагам; использует `NetworkProbes` и `HttpClient`.
- **Артефакты:** нет.
- **Метрики:** `ProbeResult` + `CheckResult`.
- **Кейсы:** проверка перед нагрузочными тестами/релизом.

## 10. Модели и контракты (ссылки на код)
- **`ITestModule`** — контракт модуля: `Id`, `DisplayName`, `Family`, `SettingsType`, `CreateDefaultSettings`, `Validate`, `RunAsync`. (`Core/Contracts/ITestModule.cs`)
- **`IRunContext`** — доступ к логам/прогрессу/артефактам/лимитам/Telegram, RunId и профилю запуска. (`Core/Contracts/IRunContext.cs`)
- **`TestOrchestrator`** — валидирует, запускает модуль, считает метрики, пишет отчёты. (`Core/Services/TestOrchestrator.cs`)
- **`ModuleRegistry`** — хранит все модули и отдаёт по `TestFamily`. (`Core/Services/ModuleRegistry.cs`)
- **`LogBus`** — асинхронная шина логов (Channel). (`Core/Services/LogBus.cs`)
- **`ProgressBus`** — публикация прогресса в UI и Telegram. (`Core/Services/ProgressBus.cs`)
- **`MetricsCalculator`** — метрики p50/p95/p99, TopSlow, ErrorBreakdown. (`Core/Services/Metrics/MetricsCalculator.cs`)
- **`ArtifactStore`** — файловое хранилище артефактов прогонов (`runs/{RunId}/...`) и профилей (`profiles/`). (`Infrastructure/Storage/ArtifactStore.cs`)
- **`TelegramPolicy`** — логика уведомлений, rate-limit. (`Infrastructure/Telegram/TelegramPolicy.cs`)
- **`TelegramNotifier`** — отправка сообщений/файлов в Telegram API. (`Infrastructure/Telegram/TelegramNotifier.cs`)
- **Доменные модели отчёта:** `TestReport`, `ResultBase`, `RunResult`, `CheckResult`, `ProbeResult`, `TimingResult`, `MetricsSummary`. (`Core/Domain/*`)

## 11. Взаимодействия (Mermaid)

### Sequence: Start → модуль → отчёт → Telegram
```mermaid
sequenceDiagram
    participant UI as MainWindowViewModel
    participant TP as TelegramPolicy
    participant OR as TestOrchestrator
    participant MOD as ITestModule
    participant AR as ArtifactStore
    UI->>TP: NotifyStartAsync(moduleName, runId)
    UI->>OR: RunAsync(module, settings, context, ct)
    OR->>MOD: Validate(settings)
    OR->>MOD: RunAsync(settings, context, ct)
    MOD-->>OR: TestReport
    OR->>AR: SaveJsonAsync/SaveHtmlAsync
    OR-->>UI: Report saved
    UI->>TP: NotifyFinishAsync(report)
```

### Flowchart: Orchestrator pipeline
```mermaid
flowchart TD
    A[MainWindowViewModel.StartAsync] --> B[Create RunContext]
    B --> C[TestOrchestrator.RunAsync]
    C --> D{Validate settings}
    D -- errors --> E[Create error TestReport]
    D -- ok --> F[ITestModule.RunAsync]
    F --> G[MetricsCalculator.Calculate]
    E --> H[FinalizeReportAsync]
    G --> H[FinalizeReportAsync]
    H --> I[JsonReportWriter/HtmlReportWriter]
    I --> J[ArtifactStore (runs/{RunId}/...)]
```

## 12. Отчёты и артефакты
- **Где создаются:**
  - Артефакты сохраняются в `runs/{RunId}/` внутри каталога данных (по умолчанию AppData). (`Infrastructure/Storage/ArtifactStore.cs`)
  - `profiles/` создаётся для будущих расширений (профили хранятся в SQLite).
- **JSON формат (структура):**
  - `run`, `environment`, `profile`, `summary`, `details`, `artifacts`. (`Core/Services/ReportWriters/JsonReportWriter.cs`)
- **Пример JSON (валидный, сокращённый):**
```json
{
  "run": { "runId": "...", "moduleType": "http.functional", "testName": "Smoke", "status": "Success" },
  "environment": { "os": "...", "appVersion": "1.0.0.0", "machineName": "PC" },
  "profile": { "parallelism": 2, "mode": "iterations", "htmlReportEnabled": false },
  "summary": { "totalDurationMs": 120.5, "totalItems": 5, "failedItems": 0, "averageMs": 120.5 },
  "details": [ { "key": "Example", "status": "Success", "durationMs": 120.5 } ],
  "artifacts": [ { "type": "JsonReport", "relativePath": "report.json" } ]
}
```
- **HTML отчёт:** содержит сводку, список проблем и ссылки на ключевые артефакты. (`Core/Services/ReportWriters/HtmlReportWriter.cs`)

## 13. Telegram интеграция
- **Настройки в UI:** токен/ChatId и флаги уведомлений доступны в окне **Настройки**.
- **Что нужно заполнить:** `BotToken`, `ChatId`, `Enabled`.
- **Когда отправляет:**
  - Старт: `NotifyOnStart`.
  - Прогресс: `ProgressMode` (реализован режим `EveryNRuns`).
  - Завершение: `NotifyOnFinish`.
  - Ошибка: `NotifyOnError`.
- **Rate limit:** `RateLimitSeconds` — минимальный интервал между сообщениями. (`Infrastructure/Telegram/TelegramPolicy.cs`)
- Ошибка Telegram не влияет на итог прогона — фиксируется отдельно. (`Infrastructure/Telegram/TelegramPolicy.cs`)

## 14. Ограничения и безопасные лимиты
- **Limits (по умолчанию):**
  - `MaxUiConcurrency = 50`
  - `MaxHttpConcurrency = 50`
  - `MaxRps = 100`
  - `MinAvailabilityIntervalSeconds = 5`
  - `MaxAvailabilityDurationMinutes = 30`
- Ограничения применяются в UI/HTTP модулях через `Min(Concurrency, Limits.*)` и `Min(RpsLimit, MaxRps)`.
- В `AvailabilityModule` лимиты `MinAvailabilityIntervalSeconds` и `MaxAvailabilityDurationMinutes` пока не используются напрямую.

> Используйте только для собственных систем или с разрешения владельца.

## 15. Troubleshooting
- **Playwright: "browsers not found"** — модули UI проверяют наличие браузеров и завершаются с ошибкой. Убедитесь, что папка `./browsers` содержит скачанные браузеры рядом с бинарником.
- **HTML/JSON отчёты не появляются** — проверьте права на запись в каталог данных (AppData) и папку `runs/`.
- **UI не обновляет лог** — `LogBus` читает через `ReadLogAsync`; убедитесь, что `Dispatcher` работает (см. `MainWindowViewModel.ReadLogAsync`).
- **Telegram не отправляет** — проверьте `Enabled`, `BotToken`, `ChatId` и `RateLimitSeconds`.
- **Items/Binding ошибки** — убедитесь, что добавлены DataTemplates в `App.axaml` и корректные ViewModels/Views.

## 16. Как расширять: добавить новый модуль
**Шаги:**
1. Создайте новый класс модуля в `Modules/<YourModule>` и реализуйте `ITestModule`.
2. Создайте настройки (Settings class) и реализуйте `Validate`.
3. Добавьте ViewModel настроек в `Presentation/ViewModels/SettingsViewModels` (наследник `SettingsViewModelBase`).
4. Создайте View в `Presentation/Views/SettingsViews` и зарегистрируйте DataTemplate в `App.axaml`.
5. Зарегистрируйте модуль в `MainWindowViewModel`:
   - добавить экземпляр в массив `modules`;
   - добавить кейс в `CreateModuleItem` switch для связки SettingsViewModel.
6. Проверьте, что модуль:
   - уважает `CancellationToken`;
   - пишет результаты в `TestReport.Results`;
   - использует `ctx.Progress.Report` для UI/Telegram;
   - сохраняет артефакты через `IArtifactStore`.

**Чек-лист готовности:**
- [ ] `Validate` возвращает список ошибок.
- [ ] `RunAsync` безопасно завершает при отмене.
- [ ] `ResultBase` заполнен корректно (DurationMs, ErrorType/Message).
- [ ] Используются лимиты `ctx.Limits`.
- [ ] Отчёты корректно сохраняются (`TestOrchestrator`).

## 17. Лицензия / авторы / контакты
Информация о лицензии и авторах отсутствует в репозитории.

## Несоответствия (требует актуализации)
- Нет актуальных несоответствий.
- В docs/03 и docs/07 HTML отчёт указан как опциональный, но в коде HTML сохраняется всегда.

---

## Дополнительно: UI и тема
- Глобальная тема — `FluentTheme` без `Mode`; `RequestedThemeVariant=Light`. (`App.axaml`)
- Базовые стили панелей, логов, текстовых полей определены в `App.axaml`.

## Скриншоты
В репозитории нет скриншотов UI. Можно добавить в `docs/images` при необходимости.

## Примерные пресеты (без выхода за лимиты)
- **Smoke (UI сценарий):** `TotalRuns=1`, `Concurrency=1`, `Headless=true`, один шаг `WaitForSelector`.
- **Нагрузка 50 VU (HTTP производительность):** `Concurrency=50`, `TotalRequests=200`, `RpsLimit=100` (в пределах `Limits.MaxHttpConcurrency` и `Limits.MaxRps`).

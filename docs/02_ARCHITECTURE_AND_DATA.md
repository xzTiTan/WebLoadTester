# Архитектура, данные и карта кода — WebLoadTester

**Версия:** v1.11 16.02.2026

**Назначение:** единый документ вместо «техспеки + карты кода»: слои, ключевые классы, потоки данных, SQLite и артефакты.

## 1. Фактическая структура решения
Проект: `WebLoadTester` (net8.0, Avalonia 11).

Папки (как в репозитории):
- `Presentation/` — UI (Views *.axaml) и ViewModels (CommunityToolkit.Mvvm)
- `Core/` — домен и контракты, оркестратор и writers
- `Modules/` — 10 модулей (ITestModule)
- `Infrastructure/` — SQLite, артефакты, настройки, Telegram, Playwright-factory
- `docs/` — документация

## 2. Правила зависимостей (слои)
- `Presentation` зависит от `Core`, `Modules`, `Infrastructure`.
- `Modules` зависят только от `Core.Contracts` и `Core.Domain`.
- `Infrastructure` реализует контракты `Core` (store/artifacts/telegram).

Цель: UI не «протекает» в модули; модули не знают про Avalonia.

## 3. Контракты и ядро
### 3.1 ITestModule
Файл: `Core/Contracts/ITestModule.cs`
- `Id`, `Family`, `DisplayName`
- `CreateDefaultSettings()`
- `Validate(settings, profileSnapshot)` — вернуть список ошибок (строки)
- `ExecuteAsync(settings, ctx, ct)` — одна итерация

### 3.2 RunContext
Файл: `Core/Services/RunContext.cs`
Содержит:
- `ILogSink` (композитный: UI + файл)
- `IProgressBus`
- `IArtifactStore`
- `Limits` (safe defaults)
- `ITelegramNotifier?`
- метаданные прогона: `RunId`, `TestCaseId`, `TestCaseVersion`, snapshot профиля

### 3.3 Оркестратор прогона
Файл: `Core/Services/RunOrchestrator.cs`
Ответственность:
1) Validate
2) (опционально) Preflight
3) Execute (iterations/duration/parallelism)
4) Persist (SQLite)
5) Report (JSON всегда, HTML по флагу)

Важно: таймаут итерации применяется через linked CTS (без «вечного» зависания).

## 4. Хранение данных и артефакты
### 4.1 AppSettings
Файл: `Infrastructure/Storage/AppSettingsService.cs`
- `%LocalAppData%/WebLoadTester/settings.json`
- `DataDirectory` → `DatabasePath = DataDirectory/webloadtester.db`
- `RunsDirectory` → корень артефактов

### 4.2 ArtifactStore
Файл: `Infrastructure/Storage/ArtifactStore.cs`
- `RunsRoot` = `RunsDirectory`
- `ProfilesRoot` = `DataDirectory/profiles` (служебно; профили запуска хранятся в SQLite)

Артефакты прогона:
- `runs/{RunId}/report.json` — всегда
- `runs/{RunId}/report.html` — если `RunProfile.HtmlReportEnabled=true`
- `runs/{RunId}/logs/run.log`
- `runs/{RunId}/screenshots/*`

### 4.3 SQLite (IRunStore)
Файл: `Core/Contracts/IRunStore.cs`, реализация: `Infrastructure/Storage/SqliteRunStore.cs`

Таблицы (MVP):
- `TestCases` — конфигурации модулей (логический «тест»)
- `TestCaseVersions` — версии конфигов (payloadJson)
- `RunProfiles` — профили запуска
- `TestRuns` — факт прогона (RunId, ссылки, статус, времена)
- `TestRunItems` — результаты единиц работы (шаг/запрос/проверка)
- `Artifacts` — относительные пути артефактов

Связи:
- `TestCases 1—N TestCaseVersions`
- `TestCaseVersions` используется при создании `TestRuns` (фиксируем `TestCaseId + Version`)
- `TestRuns 1—N TestRunItems`
- `TestRuns 1—N Artifacts`

**Важный guardrail SQLite:** параметры команд не должны оставаться `null`. Вставки используют `DBNull.Value` для nullable полей (см. `DbValue(...)` в `SqliteRunStore`).

## 5. Поток данных «UI → Run → Report»
Фактический путь выполнения (UI сценарий аналогичен остальным):
1) Пользователь вводит поля и/или загружает конфиг.
2) `ModuleConfigViewModel.EnsureConfigForRunAsync()` гарантирует существование `TestCase` и версии.
3) `MainWindowViewModel.StartAsync()`:
   - вызывает `_orchestrator.Validate(...)` до старта
   - создаёт `RunId`, `RunContext`, лог-синки
   - вызывает `_orchestrator.StartAsync(...)`
4) `RunOrchestrator`:
   - пишет `TestRuns`/`TestRunItems`/`Artifacts` в SQLite
   - сохраняет `report.json` (и `report.html`, если включено)
5) Вкладка «Прогоны» читает из `IRunStore` и открывает артефакты по `RunsRoot`.

## 6. Карта кода (ключевые точки входа)
- Запуск приложения: `Program.cs` → `App.axaml` / `App.axaml.cs`
- Главное окно: `Presentation/Views/MainWindow.axaml`
- Главная VM: `Presentation/ViewModels/MainWindowViewModel.cs`
- Workspace модулей: `Presentation/ViewModels/ModuleItemViewModel.cs`, `ModuleConfigViewModel.cs`
- Профили запуска: `Presentation/ViewModels/RunProfileViewModel.cs` + `SqliteRunStore` (RunProfiles)
- Прогоны: `Presentation/ViewModels/Tabs/RunsTabViewModel.cs`
- Оркестратор: `Core/Services/RunOrchestrator.cs`
- Отчёты: `Core/Services/ReportWriters/JsonReportWriter.cs`, `HtmlReportWriter.cs`
- SQLite: `Infrastructure/Storage/SqliteRunStore.cs`
- Артефакты: `Infrastructure/Storage/ArtifactStore.cs`

- A2 `ui.snapshot`: один проход по списку Targets (`Url/Selector/Name`) с сохранением снимков в `runs/{RunId}/screenshots/`, `ScreenshotPath` + `DetailsJson` идут в Results/RunItems.
- A3 `ui.timing`: один проход по списку Targets URL; измеряется `totalMs` и best-effort navigation timings из `performance.getEntriesByType('navigation')`, данные сериализуются в `DetailsJson`.

## 7. Известные точки стабильности (последние фиксы)
- Ранний pre-check перед стартом: `_orchestrator.Validate(...)` вызывается до `IsRunning=true`.
- Защита от NRE при создании VM: `RunProfile` создаётся до `ModuleItemViewModel`.
- Исправление SQLite `Value must be set`: nullable параметры биндим через `DBNull.Value`.
- Ошибки старта видимы пользователю: `StatusText = "Ошибка запуска: ..."` и полный stack trace в лог.

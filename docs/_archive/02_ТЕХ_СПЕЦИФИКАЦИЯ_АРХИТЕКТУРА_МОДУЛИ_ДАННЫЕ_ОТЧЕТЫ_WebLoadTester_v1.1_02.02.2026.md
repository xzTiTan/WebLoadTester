
# Техническая спецификация — архитектура, модули, данные, отчёты

**Версия:** v1.2 08.03.2026

## 0. Связанные документы
- Канон требований: `01_КАНОН_ПРОЕКТА_И_ТЗ...`
- UI-спецификация: `03_UI_СПЕЦИФИКАЦИЯ...`
- Карта кода: `04_КАРТА_КОДА_И_СВЯЗИ...`
- Глоссарий: `05_ГЛОССАРИЙ...`

---

## 1. Структура решения (по факту репозитория)
Проект собран как приложение `WebLoadTester` (net8.0) с логической декомпозицией по папкам:

- `Presentation/` — UI (Avalonia XAML) + ViewModels (CommunityToolkit.Mvvm)
- `Modules/` — реализованные тестовые модули (10 модулей)
- `Core/` — доменные сущности, контракты, сервисы ядра (оркестратор, registry)
- `Infrastructure/` — SQLite, артефакты, настройки приложения, Telegram
- `docs/` — якорные файлы

> Если в будущем понадобится «чистая» модульность на уровне проектов — можно вынести `Core`, `Modules`, `Infrastructure`, `Presentation` в отдельные csproj. Сейчас это необязательно для ВКР.

---

## 2. Архитектурные принципы и правила зависимостей
### 2.1 Принципы
- UI не содержит бизнес-логики модулей.
- Модуль реализует контракт `ITestModule` и возвращает результаты без знаний о UI.
- Ядро управляет жизненным циклом прогона и стандартизирует вывод в отчёты/БД.
- Инфраструктура реализует хранение/артефакты/настройки/Telegram.

### 2.2 Направление зависимостей (логическое)
`Presentation` → `Core` + `Modules` + `Infrastructure`

`Modules` → `Core.Contracts` + `Core.Domain`

`Infrastructure` → `Core.Contracts` + `Core.Domain`

`Core` → только свои подпапки (Domain/Contracts/Services)

---

## 3. Контракты ядра (минимум)
### 3.1 ITestModule
Файл: `Core/Contracts/ITestModule.cs`

Семантика:
- `Id` — стабильный идентификатор типа модуля (например `ui.scenario`, `http.performance`).
- `Family` — семейство (UiTesting/HttpTesting/NetSec).
- `DisplayName` — отображаемое имя.
- `CreateDefaultSettings()` — создаёт объект настроек модуля.
- `Validate(settings)` — синхронная валидация настроек.
- `ExecuteAsync(settings, ctx, ct)` — выполняет одну итерацию прогона и возвращает `ModuleResult`.

### 3.2 RunContext / IRunContext
Файл: `Core/Services/RunContext.cs`

`RunContext` содержит:
- `Log` (ILogSink) — лог в UI и файл.
- `Progress` (IProgressBus) — прогресс выполнения.
- `Artifacts` (IArtifactStore) — сохранение скриншотов/отчётов.
- `Limits` — лимиты (safe defaults).
- `Telegram` (ITelegramNotifier?) — опциональный отправитель.
- метаданные прогона: RunId, snapshot профиля, TestCaseVersion и т.д.

### 3.3 Хранилище прогонов (IRunStore)
Файл: `Core/Contracts/IRunStore.cs`, реализация: `Infrastructure/Storage/SqliteRunStore.cs`

Отвечает за:
- тесты/версии/профили,
- фиксацию запусков и результатов,
- выдачу истории прогонов.

---

## 4. Жизненный цикл прогона (стандарт)
Фактическая реализация: `Core/Services/RunOrchestrator.cs`.

### 4.1 Стадии
1) **Validate** (валидация настроек модуля)
2) (опционально) **Preflight** (быстрый набор проверок готовности)
3) **Execute** (основной прогон)
4) **Persist** (запись TestRun/TestRunItem/Artifacts в SQLite)
5) **Report** (формирование `report.json` и опционально `report.html`)

### 4.2 Политика ошибок
- Ошибка модуля → статус прогона `Failed`.
- Ошибка генерации HTML → прогон остаётся `Ok/Failed` по результатам модуля, HTML просто отсутствует.
- Ошибка Telegram → прогон не меняет статус, фиксируется логом.

---

## 5. Конкурентность и режимы нагрузки
### 5.1 Единые режимы (ядро)
- **Iterations**: выполнить N итераций (N = `RunProfile.Iterations`).
- **Duration**: выполнять итерации, пока не истечёт `RunProfile.DurationSeconds`.

### 5.2 Параллельность
- `RunProfile.Parallelism` задаёт число воркеров.
- Реализация в оркестраторе: через `SemaphoreSlim`/`Task.WhenAll` (модули выполняют одну итерацию).

### 5.3 Safe defaults
См. Канон, §8.

---

## 6. Данные и хранение (SQLite + файлы)
### 6.1 Файловая структура артефактов
Реализация: `Infrastructure/Storage/ArtifactStore.cs`

- Корень: `RunsDirectory` (по умолчанию `%LocalAppData%/WebLoadTester/runs`)
- На прогон создаётся `runs/<runId>/`:
  - `report.json` (всегда)
  - `report.html` (если включено)
  - `logs/run.log`
  - `screenshots/*` (если делались)

### 6.2 Схема SQLite (по факту реализации)
Реализация: `Infrastructure/Storage/SqliteRunStore.cs`

Таблицы:
- `TestCases(id, name, moduleType, createdAt)`
- `TestCaseVersions(id, testCaseId, version, payloadJson, createdAt)`
- `RunProfiles(id, name, parallelism, mode, iterations, durationSeconds, timeoutSeconds, headless, screenshotsPolicy, htmlReportEnabled, telegramEnabled, preflightEnabled, createdAt)`
- `TestRuns(id, runId, moduleType, moduleName, testName, testCaseId, testCaseVersion, startedAt, finishedAt, status, profileJson)`
- `TestRunItems(id, testRunId, kind, name, success, durationMs, errorMessage, extraJson)`
- `Artifacts(id, testRunId, type, relativePath)`

> Примечание: `profileJson` хранит snapshot профиля запуска; `payloadJson` хранит настройки теста.

---

## 7. Форматы отчётов
### 7.1 JSON (обязательный)
Реализация: `Core/Services/ReportWriters/JsonReportWriter.cs`

Ключевые секции `report.json`:
- `run`: RunId, времена, статус, тип модуля, TestCaseId/Version
- `environment`: OS, appVersion, machineName
- `profile`: snapshot профиля (parallelism/mode/timeouts/flags)
- `summary`: total/failed/avg/p95/p99
- `details`: список результатов (kind/key/status/duration/error/extra)
- `artifacts`: список артефактов с `relativePath`

### 7.2 HTML (опциональный)
Реализация: `Core/Services/ReportWriters/HtmlReportWriter.cs`

HTML нужен для читабельного «менеджерского» просмотра:
- итоговый статус,
- таблица метрик,
- список проблем,
- список результатов.

---

## 8. Конфигурация приложения
Реализация: `Infrastructure/Storage/AppSettingsService.cs`

Файл настроек: `%LocalAppData%/WebLoadTester/settings.json`

Хранит пути:
- `DataDirectory` (и вычисляемый `DatabasePath`),
- `RunsDirectory`,
- `ProfilesDirectory`.

Telegram настройки хранятся в UI (см. `TelegramSettingsViewModel`) и влияют на `TelegramPolicy`.

---

## 9. Расширение системы (extension points)
- Добавить новый модуль: реализовать `ITestModule`, добавить SettingsViewModel + View, зарегистрировать в `MainWindowViewModel`.
- Добавить новый writer: расширить `RunOrchestrator` (или ввести общий интерфейс) и `ArtifactStore`.
- Добавить новые поля профиля: обновить `RunProfile`, `RunProfileViewModel`, `SqliteRunStore` (DDL), `JsonReportWriter`.

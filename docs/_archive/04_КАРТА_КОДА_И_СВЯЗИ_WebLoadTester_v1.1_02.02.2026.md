
# Карта кода и связи — WebLoadTester

**Версия:** v1.4 08.03.2026

## 0. Назначение
Этот файл связывает «как задумано» (01–03) с «как реализовано» (репозиторий):
- какие папки/классы отвечают за какую часть требований,
- какие основные потоки данных,
- где добавлять/менять код при расширениях.

---

## 1. Дерево ключевых директорий
- `Core/`
  - `Contracts/` — интерфейсы (`ITestModule`, `IRunStore`, репозитории `ITestCaseRepository`/`IRunProfileRepository`/`ITestRunRepository`/`IRunItemRepository`/`IArtifactRepository`, `IArtifactStore`, `ILogSink`, `ITelegramNotifier`)
  - `Domain/` — сущности (`RunProfile`, `TestReport`, `ResultBase` и др.)
  - `Services/` — `RunOrchestrator`, `ModuleRegistry`, `RunContext`, шины логов/прогресса
  - `Services/ReportWriters/` — `JsonReportWriter`, `HtmlReportWriter`

- `Modules/` (10 модулей)
  - `UiScenario/UiScenarioModule.cs`
  - `UiSnapshot/UiSnapshotModule.cs`
  - `UiTiming/UiTimingModule.cs`
  - `HttpFunctional/HttpFunctionalModule.cs`
  - `HttpPerformance/HttpPerformanceModule.cs`
  - `HttpAssets/HttpAssetsModule.cs`
  - `NetDiagnostics/NetDiagnosticsModule.cs`
  - `Availability/AvailabilityModule.cs`
  - `SecurityBaseline/SecurityBaselineModule.cs`
  - `Preflight/PreflightModule.cs`

- `Infrastructure/`
  - `Storage/SqliteRunStore.cs` — БД (DDL + CRUD)
  - `Storage/ArtifactStore.cs` — артефакты на диске
  - `Storage/AppSettingsService.cs` — settings.json (пути)
  - `Telegram/TelegramPolicy.cs`, `TelegramNotifier.cs`

- `Presentation/`
  - `Views/` — Avalonia XAML
  - `Views/SettingsViews/` — UI настроек модулей (A1–A3, B1–B3, C1–C4)
  - `Views/Tabs/RunsTab.axaml` — вкладка «Прогоны» (история, repeat)
  - `ViewModels/` — VM, вкладки, настройки
  - `ViewModels/SettingsViewModels/` — VM настроек модулей
  - `ViewModels/RunsTabViewModel.cs` — список прогонов + повтор запуска
  - `Styles/` — tokens/controls/workspace

---

## 2. Главный поток выполнения (Run Flow)
**UI:** `MainWindowViewModel.StartAsync()`
1) Определяет выбранный модуль из текущей вкладки.
2) Собирает `RunProfileSnapshot` (`RunProfileViewModel.BuildProfileSnapshot`).
3) Создаёт `RunId`.
4) Создаёт `RunContext` (лог + прогресс + лимиты + артефакты).
5) Вызывает `RunOrchestrator.StartAsync(...)`, который создаёт `runs/<runId>/`.

**Core:** `RunOrchestrator.StartAsync(...)`
- `Validate` → опционально `preflight` → `ExecuteAsync` модуля (итерации/длительность управляет оркестратор)
- создаёт `TestReport`
- пишет `TestRun` + `TestRunItems` + `Artifacts` в SQLite
- пишет `report.json` всегда, `report.html` если включено

---

## 3. Связь требований с кодом (пример)
- Требование: «JSON всегда» → `JsonReportWriter.WriteAsync` + `ArtifactStore.SaveJsonReportAsync`.
- Требование: «Telegram опционально, ошибка не влияет» → `TelegramPolicy.IsEnabled` + `MainWindowViewModel.SendTelegramAsync`.
- Требование: «4 вкладки» → `MainWindow.axaml` + `MainWindowViewModel.SelectedTabIndex`.
- Требование: «Repeat run / Runs tab» → `RunsTabViewModel`, `RunsTab.axaml`, `MainWindowViewModel.RepeatRunAsync`.

---

## 4. Где менять, если добавляем новое поле
### 4.1 Новое поле в RunProfile
1) `Core/Domain/RunEntities.cs` (`RunProfile`)
2) `Presentation/ViewModels/RunProfileViewModel.cs`
3) `Infrastructure/Storage/SqliteRunStore.cs` (DDL + read/write)
4) `Core/Services/ReportWriters/JsonReportWriter.cs` (profile section)
5) `03_UI_СПЕЦИФИКАЦИЯ...` (UX)
6) `07_МАТРИЦА_ТРАССИРУЕМОСТИ...` (требования)

### 4.2 Новый модуль
1) `Modules/<NewModule>/<NewModule>Module.cs` (ITestModule)
2) SettingsViewModel + View (Presentation)
3) Регистрация модуля в `MainWindowViewModel` массиве `modules`
4) Обновить `01/02/03/04/07`

---

## 5. Точки контроля (типовые ошибки)
- Несогласованность `Id` модуля (в UI, в БД, в отчёте) → ломает фильтры/повтор запуска.
- Изменение схемы SQLite без миграции → ошибки при чтении истории.
- Неправильные Grid/Styles в XAML → краш layout (см. ошибки AVLNxxxx).

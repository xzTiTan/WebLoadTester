# 02 — Архитектура и данные (якорный документ)

**Версия:** v3.39 23.02.2026
**Статус:** TO-BE детализация для `docs/INDEX.md`.

> Источник истины: `docs/INDEX.md`. Этот документ детализирует архитектуру, слои, данные и артефакты без изменения канонических инвариантов.

## 1. Архитектурная карта
- Entry points: `Program.cs`, `App.axaml`, `Presentation/Views/MainWindow.axaml`, `Presentation/ViewModels/MainWindowViewModel.cs`.
- Слои:
  - `Core/` — доменные модели, контракты, orchestration, отчётность.
  - `Infrastructure/` — SQLite/settings/Playwright/Telegram/storage реализации.
  - `Modules/` — 10 модулей (`ITestModule`) без зависимости от UI.
  - `Presentation/` — Avalonia MVVM (Views/ViewModels/DataTemplates).
- Граница ответственности: UI не содержит бизнес-логики; orchestration в Core; интеграции в Infrastructure.

## 2. Контракты выполнения
- `ITestModule`: `Id`, `DisplayName`, `Family`, `SettingsType`, `CreateDefaultSettings`, `Validate`, `ExecuteAsync`.
- `RunOrchestrator`:
  1) Validate profile + settings,
  2) create TestRun,
  3) prepare run folder,
  4) optional preflight,
  5) execute workers (Iterations/Duration, Parallelism, Timeout, Pause),
  6) persist artifacts + reports + DB records,
  7) finalize status/timestamps.
- Long-running operations: только `async` + `CancellationToken`.

## 3. Данные и хранилище
- Основное хранилище: SQLite.
- Логическая схема (минимум): `TestCases`, `TestCaseVersions`, `RunProfiles`, `TestRuns`, `RunItems`, `Artifacts`, `TelegramNotifications`.
- Snapshot-правила:
  - `TestRuns.ProfileSnapshotJson` хранит snapshot профиля.
  - `runs/{RunId}/report.json` хранит `moduleSettings` для repeat-run.

## 4. Артефакты и файловая структура
- `runs/{RunId}/report.json` — всегда.
- `runs/{RunId}/report.html` — опционально.
- `runs/{RunId}/logs/run.log`.
- `runs/{RunId}/screenshots/`.
- Дополнительные module artifacts регистрируются в отчёте и в `Artifacts`.

## 5. Инварианты и безопасность
- Инварианты продукта не меняются: 10 модулей / 3 семейства / 4 вкладки.
- Разрешены только легитимные и безопасные проверки; атакующие сценарии запрещены.
- Telegram опционален: ошибки Telegram не определяют финальный статус тестового прогона.

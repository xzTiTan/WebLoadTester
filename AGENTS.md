v1.1.4 28.01.2026

# WebLoadTester — инструкция для код-агента

## Project snapshot
WebLoadTester — кроссплатформенное настольное приложение на .NET 8 + Avalonia для запуска 10 модулей UI/HTTP/сетевых проверок с отчётами (JSON всегда, HTML опционально), артефактами и опциональными Telegram-уведомлениями. Архитектура модульная (Core + Infrastructure + Modules + Presentation), UI построен по MVVM, артефакты сохраняются в структуре `runs/{RunId}/`.

## Hard constraints / non-goals
- Использовать только на ресурсах заказчика или при наличии явного разрешения владельца. Не реализовывать атакующие функции. (docs/02, docs/04, docs/08)
- Сохранять безопасные значения по умолчанию и не блокировать UI на длительных операциях. (docs/02, docs/06)
- Модули должны уважать `CancellationToken`, публиковать прогресс через `IProgressSink`, логировать через `ILogSink`. (Core/Contracts, Core/Services)
- Отчёты формируются автоматически: JSON сохраняется всегда, HTML — по настройке профиля запуска. (Core/Services, Infrastructure/Storage)
- Новые функции не должны нарушать разделение слоёв (UI без бизнес-логики, оркестратор в Core, интеграции в Infrastructure). (docs/03, docs/08)

## Repo map (Core / Infrastructure / Modules / Presentation + entry points)
- **Entry points**: `Program.cs`, `App.axaml`, `Presentation/Views/MainWindow.axaml`, `Presentation/ViewModels/MainWindowViewModel.cs`.
- **Core**: контракты (`Core/Contracts`), доменные модели (`Core/Domain`), оркестратор и сервисы (`Core/Services`).
- **Infrastructure**: Playwright/HTTP/Network/Storage/Telegram (`Infrastructure/*`).
- **Modules**: 10 модулей в `Modules/*` (UI/HTTP/NetSec).
- **Presentation**: MVVM слои (`Presentation/ViewModels`, `Presentation/Views`, `Presentation/Common`).

## Architecture rules
### MVVM
- **Views**: только разметка и биндинги; без бизнес-логики и сетевых вызовов.
- **ViewModels**: управляют состоянием UI и вызывают Core/Infrastructure через контракты.
- **Services**: бизнес-логика и оркестрация — в Core/Services и Infrastructure.

### Границы слоёв
- **Core**: жизненный цикл прогона, контракты, доменные модели, метрики, отчёты.
- **Infrastructure**: Playwright/HTTP/Network/Telegram/Storage реализации.
- **Modules**: реализация `ITestModule`, не зависит от UI.
- **Presentation**: Avalonia UI, ViewModels и DataTemplates.

### Как добавить новый модуль (чек-лист)
1. Создать модуль в `Modules/<NewModule>` и реализовать `ITestModule`.
2. Добавить настройки (Settings class) и `Validate`.
3. Создать ViewModel настроек в `Presentation/ViewModels/SettingsViewModels`.
4. Создать View настроек в `Presentation/Views/SettingsViews` и DataTemplate в `App.axaml`.
5. Зарегистрировать модуль в `MainWindowViewModel` (`modules[]` + `CreateModuleItem`).
6. Проверить: `CancellationToken`, `ctx.Progress.Report`, `ctx.Log`, сохранение артефактов через `IArtifactStore`.

### Async + CancellationToken
- Все длительные операции должны быть `async` и принимать `CancellationToken`.
- UI поток не блокировать; обновления UI — через Dispatcher.

### Логирование / прогресс
- Использовать `ILogSink` (`LogBus`) и `IProgressSink` (`ProgressBus`) из `RunContext`.
- Не обходить существующие механизмы логов/прогресса.

## Reporting & artifacts
- **Отчёты**: сохраняются в `runs/{RunId}/report.json` (всегда) и `runs/{RunId}/report.html` (опционально).
- **Скриншоты**: сохраняются в `runs/{RunId}/screenshots/`.
- **Логи**: сохраняются в `runs/{RunId}/logs/run.log`.
- **Профили**: папка `profiles/` создаётся для будущих расширений (профили сейчас хранятся в SQLite).

## Playwright / browsers
- Приложение ожидает браузеры в `AppContext.BaseDirectory/browsers` (то есть рядом с бинарником).
- Установка браузеров Playwright выполняется скриптом в папке сборки (генерируется пакетом Microsoft.Playwright).

**Windows (PowerShell):**
```powershell
# после dotnet build
$env:PLAYWRIGHT_BROWSERS_PATH = (Join-Path (Get-Location) "bin/Debug/net8.0/browsers")
.\bin\Debug\net8.0\playwright.ps1 install
```

**Linux/macOS (bash):**
```bash
# после dotnet build
PLAYWRIGHT_BROWSERS_PATH=bin/Debug/net8.0/browsers \
  ./bin/Debug/net8.0/playwright.sh install
```

> Если используете Release или publish, замените путь на соответствующий `bin/Release/net8.0` или папку публикации.

## Tests & verification
- Минимум: `dotnet restore`, `dotnet build`.
- `dotnet test` — только если добавлены тестовые проекты (в репозитории их нет).

## Document/versioning rule
- При любом изменении файлов проекта **обновляйте версию и дату** в формате `vX.Y 24.01.2026`.
- Минимум это относится к `README.md` и `AGENTS.md`.

## Safety / ethics
Использовать приложение только с разрешения владельца системы и для легитимных целей. Не применять для атакующих сценариев.

## Несоответствия (требует актуализации)
- Нет актуальных несоответствий.

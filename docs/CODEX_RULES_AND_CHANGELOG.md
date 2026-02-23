v3.31 18.02.2026

# CODEX_RULES_AND_CHANGELOG — правила для Codex/агентов и журнал изменений

## 1) Source of truth
- **Единый канон продукта (TO-BE):** `docs/INDEX.md`.
- Этот файл — **только** правила работы агента + журнал изменений.
- Код и текущий UI считаются **AS-IS** и приводятся к TO-BE через §16 «Дельта внедрения» в `docs/INDEX.md`.

## 2) Жёсткие инварианты (нельзя ломать)
1. **10 модулей / 3 семейства / 4 вкладки:** UI / HTTP / Сеть и безопасность / Прогоны.
2. **Хранилище:** SQLite + файловые артефакты в `runs/{RunId}/`.
3. **Отчёты:** `report.json` всегда; `report.html` опционально.
4. **Безопасность:** никаких атакующих действий (скан уязвимостей, bruteforce, эксплойты и т.п.).
5. **UI:** русский язык, ровная сетка, без наложений; MVVM без логики в Views.
6. **Отмена:** все долгие операции — `async` + `CancellationToken`. Stop ≠ Cancel (см. INDEX).

## 3) Порядок работы агента (обязательный)
1) Прочитать `docs/INDEX.md` целиком.  
2) Составить план изменений (коротко: какие файлы/классы/вьюхи трогаем).  
3) Реализовать изменения небольшими логическими шагами.  
4) После каждого шага — компиляция (`dotnet build`).  
5) По завершении — обновить этот файл (Changelog) + при необходимости обновить `docs/INDEX.md` (если менялась спецификация).

## 4) Правила изменений кода (guardrails)
- Не переименовывать публичные контракты/идентификаторы без причины.
- Не вводить новые технологии без необходимости (никаких новых DI-фреймворков, ORM и т.п. в MVP).
- Любые изменения структуры данных/отчётов — сначала фиксируются в `docs/INDEX.md`, затем код.
- Если в коде обнаружена “историческая” сущность (например, таблица есть, UI нет) — не удалять молча:
  - либо согласовать как “TO-BE”,
  - либо оформить в INDEX как дельту с планом миграции.

## 5) Обязательное версионирование документов
При любом изменении:
- обновить версии/дату в `README.md`, `AGENTS.md`, `docs/INDEX.md` (если спецификация менялась),
- добавить запись в Changelog ниже.

---

# Changelog

## v3.37 23.02.2026
- Возвращена OLD-логика локального пути браузеров Playwright: при старте приложения устанавливается `PLAYWRIGHT_BROWSERS_PATH` в `AppContext.BaseDirectory/playwright-browsers` до первого использования Playwright.
- `PlaywrightFactory` переведён на `playwright-browsers` по умолчанию; `HasBrowsersInstalled()` проверяет Chromium в локальном каталоге по маркерам `chromium-*` и наличию бинарников/файлов chrome.
- `InstallChromiumAsync()` теперь устанавливает Chromium через `Microsoft.Playwright.Program.Main(["install", "chromium"])` с логированием выбранного `BrowsersPath`.
- UI/UX не менялся; существующие validation-ошибки UI-модулей и кнопка установки Chromium продолжают работать через обновлённую фабрику.
- Обновлены версии `README.md` и `AGENTS.md`.

## v3.36 23.02.2026
- В `ModuleWorkspaceViewModel` workspace-валидация расширена проверкой `ITestModule.Validate(settings)` для текущего выбранного модуля.
- В `SetSelectedModule` добавлено сохранение ссылки на текущий модуль (`_currentModule`), чтобы включать module-level ошибки в `WorkspaceValidationErrors`.
- При получении settings используется приоритетно `SettingsViewModelBase.Settings`, а при ином VM — fallback через свойство `Settings` (reflection) при наличии.
- Обновлены версии `README.md` и `AGENTS.md`.

## v3.35 23.02.2026
- UX запуска: в `MainWindowViewModel.StartAsync` зафиксирован порядок, при котором при validation-failure запуск немедленно завершается без перехода в Running-состояние (навигация/LeftNav остаются доступными).
- В `RunControlViewModel` добавлены прокси-свойства `InstallChromiumCommand`, `HasChromiumValidationError`, `CanInstallChromium` для отображения действия установки Chromium в зоне валидации.
- В `RunControlView.axaml` добавлена кнопка «Установить Chromium», видимая только при ошибках валидации, содержащих `Chromium`, и привязанная к существующей `InstallPlaywrightBrowsersCommand`.
- Обновлены версии `README.md` и `AGENTS.md`.

## v3.34 23.02.2026
- Исправлены runtime-дефолты в settings VM: URL `https://пример.рф` заменены на `https://example.com`, а имя `пример` на `example` в `HttpAssetsSettingsViewModel`, `UiSnapshotSettingsViewModel`, `UiTimingSettingsViewModel`.
- Добавлена синхронизация списков в конструкторах: `SyncTargets()` в `UiSnapshotSettingsViewModel` и `UiTimingSettingsViewModel`, `SyncAll()` в `HttpFunctionalSettingsViewModel` для немедленного соответствия UI-строк и `_settings`.
- В `Validate()` модулей `UiSnapshot`, `UiTiming`, `UiScenario` добавлена проверка наличия Chromium через `PlaywrightFactory.HasBrowsersInstalled()` с понятной инструкцией по установке и путём browsers.
- Обновлены версии `README.md` и `AGENTS.md`.

## v3.33 23.02.2026
- Добавлен стиль `TextBlock.error` в `Presentation/Styles/Controls.axaml` с красным цветом (`Foreground=Red`) для единообразного отображения валидационных ошибок.
- Во view-файлах, выводящих `ValidationErrors`, `WorkspaceValidationErrors` и `RowErrorText`, добавлен `Classes="error"` для всех error `TextBlock`.
- Обновлены версии `README.md` и `AGENTS.md`.

## v3.32 23.02.2026
- Исправлена ложная пометка dirty при смене верхней вкладки: в `ModuleConfigViewModel` игнорируются UI-only/computed изменения `RunProfileViewModel` (включая `IsUiFamily`), поэтому переход между семьями вкладок без редактирования больше не блокируется guard-диалогом.
- В `MainWindowViewModel` добавлен диагностический лог `[NavGuard] ...` при блокировке перехода guard-механизмом для упрощения дальнейшей диагностики.
- Обновлён UX-чеклист: зафиксировано требование, что переключение вкладки не должно само выставлять dirty-состояние.
- Обновлены версии `README.md` и `AGENTS.md`.

## v3.31 18.02.2026
- Stage 2 UI-shell: добавлен новый `ModuleWorkspaceView` (3 колонки LeftNav/Workspace/Details) с рабочими `GridSplitter` и единственным вертикальным `ScrollViewer` в центральной рабочей области.
- Добавлены `ModuleFamilyViewModel` и `ModuleWorkspaceViewModel` (bridge к существующему `MainWindowViewModel`) для прокидывания списков модулей, выбранного module settings VM и состояния Running без изменения логики модулей/раннера.
- Вкладки UI/HTTP/Сеть переведены на новый workspace-контент через DataTemplate (`ModuleFamilyViewModel -> ModuleWorkspaceView`); вкладка `Прогоны` оставлена без изменений.
- В LeftNav добавлены поиск по модулям и блокировка изменения выбора при Running (UI disabled + VM guard).
- Обновлены версии `README.md` и `AGENTS.md`.

## v3.30 17.02.2026
- Выполнен визуальный refresh основного окна и общих стилей Avalonia без изменения бизнес-функционала: обновлены токены отступов, радиусов, типографики и палитры для более современного интерфейса.
- Усилена читаемость и защита от наложений в UI: увеличены минимальные размеры и отступы контролов, добавлен wrapping в badge-элементах и подправлены безопасные интервалы в tab/field/layout стилях.
- Обновлён `MainWindow` (каркас, размеры и интервалы) для более стабильного размещения элементов на рабочих разрешениях.
- Обновлены версии `docs/INDEX.md`, `README.md`, `AGENTS.md`.

## v3.29 17.02.2026
- Исправлен runtime-crash Avalonia `InvalidCastException` при открытии `MainWindow`: design token'ы `PagePadding` и `CardPadding` переведены в тип `Thickness`, чтобы динамические ресурсы корректно биндилась в `Margin/Padding`.
- README приведён к актуальной структуре документации (`docs/INDEX.md` + `docs/CODEX_RULES_AND_CHANGELOG.md`) без ссылок на удалённые файлы.
- AGENTS.md синхронизирован с текущей структурой docs и обновлён по версии/дате.







## v3.13 17.02.2026
- Prompt 13: для UI-семейства внедрены worker-scoped артефакты: `runs/{RunId}/profiles/w{WorkerId}` и `runs/{RunId}/screenshots/w{WorkerId}/it{Iteration}`.
- Добавлено сохранение snapshots на воркер: `profile.json` и `moduleSettings.json` в `profiles/w{WorkerId}` с регистрацией в artifacts-реестре отчёта.
- Скриншоты UI-модулей приведены к единому path-builder через worker/iteration и сохраняют относимые пути, совместимые с consumers отчётов.
- Добавлены оффлайн unit-тесты для path-builder и регистрации profile/moduleSettings snapshots.
- Обновлены версии `docs/INDEX.md`, `README.md`, `AGENTS.md`.

## v3.12 17.02.2026
- Prompt 12B: добавлен детерминированный переход к первой ошибке валидации (scroll + focus) при блокировке `Старт` и `Сохранить`.
- Для ключей таблиц/списков (`table.steps/targets/assets/ports`, `list.endpoints`) реализован переход к первой DataGrid/List и фокус с выбором первой строки/элемента.
- `ValidationState` расширен выбором первого видимого ключа ошибки по заданному приоритету; добавлены оффлайн тесты на порядок выбора ключа.
- Обновлены версии `docs/INDEX.md`, `README.md`, `AGENTS.md`.

## v3.11 17.02.2026
- Prompt 12A: внедрён единый UX валидации (touched + submit) для конфигурации и профиля запуска, ошибки показываются после LostFocus или после попытки Start/Save.
- Добавлены summary-баннеры ошибок в карточках «Конфигурация», «Настройки модуля», «Параметры запуска», и единый агрегированный log при блокировке запуска.
- Start блокируется при ошибках в профиле/настройках модуля, Save — при невалидном имени конфигурации (с inline-ошибкой по touched/submit).
- Добавлены оффлайн unit-тесты на ValidationState и блокировку StartCommand.
- Обновлены версии `docs/INDEX.md`, `README.md`, `AGENTS.md`.

## v3.10 17.02.2026
- Prompt 11: во всех целевых таблицах/списках модулей внедрён канон TableToolbar (WrapPanel + `wrap-gap`, без `WrapPanel.Spacing`).
- Для `ui.scenario` реализованы команды Add/Delete/Duplicate/Up/Down с сохранением выделения и проверкой границ; для остальных целевых модулей — Add/Delete/Duplicate.
- Добавлены оффлайн unit-тесты на команды TableToolbar (`ui.scenario`, `ui.snapshot`, `http.assets`).
- Обновлены версии `docs/INDEX.md`, `README.md`, `AGENTS.md`.

## v3.9 17.02.2026
- Prompt 10: ModuleWorkspace приведён к канону CRUD конфигов (`Загрузить/Сохранить/Удалить`), удалена кнопка «Перезаписать».
- Добавлен единый dirty-state (`Сохранено` / `Есть несохранённые изменения`) и guard-подтверждение при рискованных действиях.
- Добавлены оффлайн unit-тесты на версионное сохранение, dirty-state и guard-cancel.
- Обновлены версии `docs/INDEX.md`, `README.md`, `AGENTS.md`.

## v3.8 17.02.2026
- Prompt 9: реализованы Telegram progress-уведомления по `ProgressMode` (`Off/EveryN/EveryTSeconds`) в runtime без изменения схемы настроек.
- Прогресс-уведомления учитывают per-run флаг профиля, глобальный `Enabled`, `RateLimitSeconds` и не влияют на итог статуса прогона при ошибках отправки.
- Добавлены оффлайн unit-тесты на прогресс-режимы, rate-limit и safe-failure поведение (`TelegramProgressTests`).
- Обновлены версии `docs/INDEX.md`, `README.md`, `AGENTS.md`.

## v3.7 17.02.2026
- Prompt 8B: реализованы run-уведомления Telegram (start/finish/error) с безопасной обработкой ошибок и без влияния на итог прогона.
- Добавлен rate limit отправки по `RateLimitSeconds` в тестируемом виде (`TimeProvider`).
- Добавлен индикатор Telegram `Выкл/Ок/Ошибка` с tooltip в верхней панели MainWindow и открытием Settings по клику.
- Добавлены оффлайн unit-тесты на routing/gating/rate-limit Telegram-уведомлений.
- Обновлены версии `README.md`, `AGENTS.md`, `docs/INDEX.md`.

## v3.6 17.02.2026
- Prompt 8A: добавлен MVP-раздел Telegram в SettingsWindow (Enabled/BotToken/ChatId, notify-флаги, ProgressMode/ProgressEveryN/RateLimitSeconds, кнопка тестовой отправки и статус результата).
- Реализована персистентность Telegram-настроек в `settings.json` через `AppSettingsService` (`AppSettings.Telegram`).
- Добавлены `ITelegramClient` + `TelegramClient` (HttpClient, безопасный результат успех/ошибка без выброса исключений наружу) и оффлайн unit-тесты на endpoint/payload/401/500.
- Обновлены версии `README.md`, `AGENTS.md`, `docs/INDEX.md`.

## v3.5 17.02.2026
- Prompt 7B follow-up: доработана вкладка «Прогоны» (детали профиля с поддержкой `profile.timeouts.operationSeconds` и legacy `timeoutSeconds`).
- В блоке «Топ ошибок» добавлен вывод счётчика повторений для каждой ошибки.
- Добавлен unit-test на парсинг legacy `timeoutSeconds` в Repeat Run snapshot.
- Обновлены версии `README.md` и `AGENTS.md` по правилу versioning.

## v3.4 16.02.2026
- Prompt 5: доработан UI-family MVP (`ui.scenario`, `ui.snapshot`, `ui.timing`) под TO-BE из `docs/INDEX.md` (§1.1, §8.A).
- Playwright path перенесён в `{DataDirectory}/browsers`, установка Chromium теперь стримит вывод в лог и баннер показывается для всех UI-модулей.
- В UI-настройках модулей добавлены wrap-safe TableToolbar-панели (Add/Delete/Duplicate, plus Up/Down только для сценария).
- Обновлены версии `README.md` и `AGENTS.md` по правилу versioning.

## v3.3 16.02.2026
- Консолидация документации: `docs/INDEX.md` — единственный исходный документ.
- Оставлены только `docs/INDEX.md` и `docs/CODEX_RULES_AND_CHANGELOG.md` (прочие docs удалены/перенесены в историю git).
- Удалены устаревшие/кривые UI mockups и архивы (вариант A).
- Обновлены `README.md` и `AGENTS.md` под новый канон.

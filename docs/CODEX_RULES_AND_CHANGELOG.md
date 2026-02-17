v3.9 17.02.2026

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

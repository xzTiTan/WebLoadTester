# 05 — План тестирования и трассируемость (якорный документ)

**Версия:** v3.39 23.02.2026
**Статус:** TO-BE детализация для `docs/INDEX.md`.

## 1. Цель тест-плана
- Подтвердить соблюдение инвариантов и ключевых сценариев MVP.
- Обеспечить трассируемость: требование (INDEX) → проверка → ожидаемый результат.

## 2. Обязательные smoke-проверки
1. Запуск приложения и инициализация инфраструктуры (БД/папки/настройки).
2. Запуск модуля в `Iterations` и `Duration` режимах.
3. Stop/Cancel и корректная финализация статуса.
4. Сохранение `report.json` (всегда) и `report.html` (если включён).
5. Наличие `workerId`/`iteration` в items отчёта.
6. Repeat-run из `report.json` без автозапуска.
7. Telegram optional path: ошибки уведомлений не ломают run status.

## 3. Regression-набор (минимум)
- Навигация между модулями до/во время/после run.
- Валидация профиля запуска (`Parallelism`, `Iterations/Duration`, `Timeout`, `PauseBetweenIterationsMs`).
- Артефакты: logs/screenshots/report paths.
- CRUD конфигураций и восстановление snapshot.

## 4. Трассируемость (примерная матрица)
- INDEX §0.1 (10/3/4) → UI smoke по вкладкам/модулям.
- INDEX §0.2 (RunProfile, Stop≠Cancel) → orchestration/unit-smoke.
- INDEX §0.3 (storage/artifacts) → filesystem + DB checks.
- INDEX §4.4 (`PauseBetweenIterationsMs`) → timing semantics + snapshot checks.
- INDEX §6 (UI canon) → visual/layout/no-overlap smoke.

## 5. Команды верификации
- `dotnet restore`
- `dotnet build`
- `dotnet test` (если есть тестовые проекты в репозитории)
- По документации: `rg`-проверки битых ссылок и соответствия якорных документов.

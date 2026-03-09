# Block 1 — Приемочный аудит 10 модулей (статический E2E аудит)

**Версия:** v3.49 09.03.2026  
**Статус:** выполнен как статический аудит (без runtime-прогонов из-за отсутствия `dotnet` в среде).

## 0) Область и ограничения
- Проверен сквозной контур: `View -> ViewModel -> Settings -> Module.ExecuteAsync -> RunOrchestrator -> report.json/report.html -> Runs/Repeat-run`.
- Архитектура shell, moduleId, repeat-run контракт, структура report.json и оркестрация не менялись.
- Runtime smoke (`dotnet restore/build/run/test`) в этом окружении выполнить нельзя: `dotnet: command not found`.

## 1) Итог по 10 модулям

| # | Публичный модуль | moduleId | Статус |
|---|---|---|---|
| 1 | Дымовое тестирование | `net.preflight` | PARTIAL |
| 2 | Функциональное тестирование | `http.functional` | PARTIAL |
| 3 | Регрессионное тестирование | `ui.scenario` | PARTIAL |
| 4 | Интерфейсное тестирование | `ui.snapshot` | PARTIAL |
| 5 | Тестирование совместимости | `ui.timing` | PARTIAL |
| 6 | Тестирование производительности | `http.performance` | PARTIAL |
| 7 | Тестирование безопасности | `net.security` | PARTIAL |
| 8 | Тестирование доступности | `net.availability` | PARTIAL |
| 9 | Диагностическое тестирование | `net.diagnostics` | PARTIAL |
| 10 | Тестирование ресурсов Web-сайта | `http.assets` | PARTIAL |

> Причина `PARTIAL` для всех: статически цепочки прослеживаются, но верификация UI-рендера/реального запуска/артефактов на ФС в этой среде не подтверждена runtime-проверками.

## 2) Что подтверждено по архитектурной цепочке

### 2.1 Модули и публичная номенклатура
- В `MainWindowViewModel` зарегистрированы все 10 модулей.
- `ModuleCatalog` содержит 10 публичных наименований в учебной модели.

### 2.2 UI wiring
- В `App.axaml` есть DataTemplate для всех 10 SettingsViewModel.
- `ModuleWorkspaceView` использует единый центральный `ScrollViewer` для формы.

### 2.3 Validation wiring
- `ModuleWorkspaceViewModel` агрегирует ошибки от TestCase, RunProfile, `IValidatable` VM и `ITestModule.Validate(settings)`.
- `RunOrchestrator.Validate` добавляет профильные проверки (parallelism/mode/timeout/pause).

### 2.4 Report contract
- `JsonReportWriter` всегда пишет `moduleId`, `profile`, `moduleSettings`, `items`, `artifacts`.
- `RunOrchestrator` сохраняет JSON всегда и HTML при `HtmlReportEnabled`.
- `moduleSettings` берётся из `ModuleSettingsSnapshot` и не вырезается.

### 2.5 Runs/History/Repeat-run
- `RunsTabViewModel` валидирует повтор через `report.json` и проверяет `moduleId/profile/moduleSettings`.
- `MainWindowViewModel.RepeatRunFromReportAsync` восстанавливает module settings + RunProfile из `report.json`, с fallback на DB snapshot.

## 3) Фокусные результаты по критичным модулям

### 3.1 Регрессионное тестирование (`ui.scenario`) — PARTIAL
**Работает (статически):**
- Публичная идентичность соответствует учебной модели.
- Есть baseline compare: поиск последнего успешного `ui.scenario`-прогона с совпадающим fingerprint.
- При отсутствии baseline формируется `TestStatus.Partial` в элементе «Регрессионное сравнение».
- В HTML есть специальный блок «Регрессионное сравнение».

**Риски/дефекты:**
- P1: baseline-поиск опирается только на `report.json` в `runs/*`; при повреждённом/отсутствующем JSON baseline не найдётся даже при наличии DB-данных.

### 3.2 Тестирование совместимости (`ui.timing`) — PARTIAL
**Работает (статически):**
- Настройки профиля включают browser channel, viewport, user-agent, optional headless override.
- Применение профиля в рантайме есть (`SetViewportSizeAsync`, `UserAgent`, профиль в `DetailsJson`).
- В JSON отчёте поля профиля сохраняются через `detailsJson`/`metrics`.
- В HTML есть «Матрица результатов», где отображается summary по элементам.

**Риски/дефекты:**
- P2: в комментарии класса осталась legacy-семантика «замеров времени», что конфликтует с публичной подачей как compatibility.

### 3.3 Тестирование производительности (`http.performance`) — PARTIAL
**Работает (статически):**
- Валидация endpoint-параметров есть.
- Метрики отчёта (`average/p95/p99`) считаются на уровне report summary.
- HTML summary выводит P95/P99.

**Риски/дефекты:**
- P1: модуль агрегирует результаты по endpoint-циклу, но без отдельной специализированной секции percentile/throughput per endpoint в HTML; для защиты это может выглядеть «слишком общо».

### 3.4 Тестирование безопасности (`net.security`) — PARTIAL
**Работает (статически):**
- Проверки безопасные (headers/redirect/cookies), без атакующих функций.
- severity/recommendations прокидываются через result fields и HTML recommendations.

**Риски/дефекты:**
- P1: в случае глобальной недоступности URL формируется общий fail, но детализация по каждому активированному check не строится (снижение диагностичности).

### 3.5 Интерфейсное тестирование (`ui.snapshot`) — PARTIAL
**Работает (статически):**
- Скриншотный flow полноценный: selector screenshot / page screenshot, сохранение через `IArtifactStore`.
- Пути скриншотов попадают в artifacts/report.

**Риски/дефекты:**
- P1: `ScreenshotFormat` в настройках фактически не влияет на формат сохранения (в коде зафиксирован PNG).

## 4) Статус по каждому модулю (кратко)

### 1) Дымовое тестирование (`net.preflight`) — PARTIAL
- Проверено: settings/validate/execute/progress/results/report wiring.
- Работает: preflight checks DNS/TCP/TLS/HTTP + environment checks.
- Не подтверждено runtime: фактический прогон и визуальный UX.
- Дефекты: P2 (нет).
- Нужны правки кода: нет срочно.
- Риск для демонстрации: низкий.

### 2) Функциональное тестирование (`http.functional`) — PARTIAL
- Проверено: endpoint settings + assertions + result items.
- Работает: validate + run + progress + endpoint results.
- Не подтверждено runtime: реальное поведение assert на живом API.
- Дефекты: P2 (нет).
- Нужны правки кода: нет срочно.
- Риск: средний (без runtime smoke).

### 3) Регрессионное (`ui.scenario`) — PARTIAL
- Проверено: baseline compare, no-baseline flow, report/html embedding.
- Работает: regression item + Partial semantics when baseline missing.
- Не подтверждено runtime: Playwright сценарии на реальном браузере.
- Дефекты: P1 (baseline только из JSON-файлов runs).
- Нужны правки: да, отдельным fix-pack.
- Риск: средний.

### 4) Интерфейсное (`ui.snapshot`) — PARTIAL
- Проверено: targets, selector/page screenshot, artifacts/report.
- Работает: screenshot paths + result items.
- Не подтверждено runtime: визуальные артефакты и файловая запись.
- Дефекты: P1 (`ScreenshotFormat` не применяется).
- Нужны правки: да, точечные.
- Риск: средний.

### 5) Совместимость (`ui.timing`) — PARTIAL
- Проверено: browser/profile/viewport/user-agent/headless wiring.
- Работает: profile в details/metrics, matrix table в HTML.
- Не подтверждено runtime: межбраузерная фактическая совместимость.
- Дефекты: P2 (legacy wording про «тайминги» в class summary).
- Нужны правки: да, документарно/текстово.
- Риск: средний.

### 6) Производительность (`http.performance`) — PARTIAL
- Проверено: run/results/report summary percentile.
- Работает: endpoint checks + aggregated p95/p99.
- Не подтверждено runtime: нагрузочное поведение под реальным трафиком.
- Дефекты: P1 (нет специализированной пер-endpoint perf-сводки в HTML).
- Нужны правки: желательно.
- Риск: средний.

### 7) Безопасность (`net.security`) — PARTIAL
- Проверено: non-attacking checks + severity/recommendations.
- Работает: baseline security checks с безопасной моделью.
- Не подтверждено runtime: реальные ответы заголовков/редиректов.
- Дефекты: P1 (при global fail не разворачивается детализация по всем check).
- Нужны правки: желательно.
- Риск: средний.

### 8) Доступность (`net.availability`) — PARTIAL
- Проверено: HTTP/TCP режимы, validate, status/result.
- Работает: single-check per iteration + metrics.
- Не подтверждено runtime: стабильность сетевых ошибок/таймаутов.
- Дефекты: P2 (нет).
- Нужны правки: нет срочно.
- Риск: низкий.

### 9) Диагностика (`net.diagnostics`) — PARTIAL
- Проверено: DNS/TCP/TLS probes, progress and metrics.
- Работает: itemized diagnostics with itemIndex/worker/iteration.
- Не подтверждено runtime: сетевые probes в среде запуска.
- Дефекты: P2 (нет).
- Нужны правки: нет срочно.
- Риск: низкий.

### 10) Ресурсы Web-сайта (`http.assets`) — PARTIAL
- Проверено: asset assertions (type/size/latency), result items, serialization.
- Работает: per-asset checks + progress + status.
- Не подтверждено runtime: реальные ответы по ассетам.
- Дефекты: P2 (нет).
- Нужны правки: нет срочно.
- Риск: низкий.

## 5) Единый список дефектов (P0/P1/P2)

### P0
- Не выявлено статическим аудитом.

### P1
1. `ui.scenario`: baseline compare зависит от наличия корректных historical `runs/*/report.json`; fallback к DB для baseline-сопоставления отсутствует.
2. `ui.snapshot`: поле `ScreenshotFormat` не влияет на фактический формат (сохраняется PNG).
3. `http.performance`: HTML не содержит специализированной per-endpoint performance-сводки (только общий summary/матрица).
4. `net.security`: при глобальном падении запроса нет детализированных результатов по каждому включённому check.

### P2
1. `ui.timing`: legacy-формулировка в summary класса («замеров времени») может путать относительно публичной semantics «совместимость».
2. Верхние заголовки вкладок в shell (`UI/HTTP/Сеть`) не совпадают с формулировками из публичной модели в документах (скорее UX-текстовый долг).

## 6) Безопасные фиксы в Block 1
- Кодовых фиксов не вносилось (аудит-only), чтобы не смешивать приемку и исправления.

## 7) Рекомендуемый порядок fix-pack
1. **P1-functional:** baseline fallback (`ui.scenario`) + `ScreenshotFormat` применение (`ui.snapshot`).
2. **P1-reporting:** perf HTML секция per-endpoint + security detailed failure decomposition.
3. **P2-text/ux:** унификация публичных формулировок (tabs/summary/comments) без изменения moduleId/контрактов.

## 8) Следующие 3–5 задач для Codex-промптов
1. `ui.scenario`: добавить безопасный fallback baseline-выборки из DB snapshot/RunItems при отсутствии/битом `report.json`.
2. `ui.snapshot`: довести `ScreenshotFormat` до реального выбора формата (png/jpeg) без изменения report.json-контракта.
3. `http.performance`: добавить в HTML отчёт явную таблицу метрик по endpoint (avg/p95/p99/fail-rate).
4. `net.security`: при global request fail формировать synthetic per-check результаты с одинаковой причиной.
5. UX/doc pass: унифицировать заголовки верхних вкладок с учебной номенклатурой без архитектурных изменений.

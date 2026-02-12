# План испытаний и трассируемость — WebLoadTester

**Версия:** v1.1 11.02.2026

**Назначение:** объединяет «план испытаний» и «матрицу трассируемости» в одном файле.

## 1. Smoke-проверки (для каждого PR)
1) `dotnet clean && dotnet build -c Release`.
2) Запуск приложения.
3) Для одного модуля из каждого семейства (A/B/C):
   - загрузить или создать конфиг,
   - пройти валидацию,
   - выполнить запуск и получить RunId,
   - открыть `runs/{RunId}/report.json`,
   - убедиться, что запись появилась в «Прогоны».

## 2. Демонстрационные сценарии (по модулям)
### A1 ui.scenario
- Ввести Name/Description.
- Добавить 2 шага:
  1) `Selector = ...`, `Text = ''` (Click)
  2) `Selector = ...`, `Text = 'abc'` (Fill)
- Старт → отчёт + (при политике) скрин.

### A2 ui.snapshot
- Список URL (минимум 2).
- Старт → скриншоты в `screenshots/`, summary в отчёте.

### A3 ui.timing
- URL + iterations=3.
- Старт → метрики времени загрузки.

### B1 http.functional
- Endpoint + метод + expected status.
- Старт → pass/fail по ассерциям.

### B2 http.performance
- Endpoint + iterations/duration, параллельность.
- Старт → метрики latency/throughput.

### B3 http.assets
- BaseUrl + список путей ассетов.
- Старт → список невалидных ассетов.

### C1 net.diagnostics
- Hostname.
- Старт → результаты DNS/TCP/TLS этапов.

### C2 net.availability
- Host + interval (если есть) / iterations.
- Старт → ok/fail probe.

### C3 net.security
- URL.
- Старт → findings по baseline (заголовки/минимальный TLS).

### C4 net.preflight
- URL/Host.
- Включить Preflight в профиле.
- Старт любого модуля → preflight выполняется до основного.

## 3. Трассируемость (требование → реализация → испытание)
| Требование | Где описано | Где реализовано | Чем проверяем |
|---|---|---|---|
| 4 вкладки верхнего уровня | 01, 03 | `MainWindow.axaml` + VM | Smoke + UI обзор |
| 10 модулей / 3 семейства | 01, 02 | `Modules/*` + `ModuleRegistry` | Демонстрация по модулям |
| SQLite обязателен | 01, 02 | `SqliteRunStore` | Smoke: запись Run + список Runs |
| runs/{RunId}/report.json всегда | 01, 02 | `ArtifactStore` + `JsonReportWriter` | Smoke: проверить наличие файла |
| report.html по флагу | 01, 02 | `HtmlReportWriter` | Включить флаг и проверить |
| Telegram опционально | 01, 02, 03 | `TelegramPolicy` | Выкл/вкл; ошибка не ломает run |
| Preflight опционально | 01, 02, 03 | `PreflightModule` + orchestrator | Включить флаг и увидеть этап |
| Запрет атак | 01 | Модули C3/C4 | Code review + сценарии |

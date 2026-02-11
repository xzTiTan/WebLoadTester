
# План испытаний и демонстрационные сценарии — WebLoadTester

**Версия:** v1.8 11.02.2026

**Назначение:** закрепляет проверки для приёмки и демонстрации.  
**См. также:** [01_CANON.md](01_CANON.md), [02_TECH_SPEC.md](02_TECH_SPEC.md), [07_TRACEABILITY.md](07_TRACEABILITY.md).

## 0. Назначение
План испытаний фиксирует набор проверок, которые:
- подтверждают критерии приёмки (см. `01_CANON.md`),
- служат демонстрационным материалом для защиты ВКР.

---

## 1. Общие испытания (для всего приложения)
1) Создание RunId и папки `runs/<runId>/` для любого запуска.
2) Формирование `report.json` при любом исходе (Ok/Failed).
3) Запись прогона в SQLite и отображение в «Прогоны».
4) Отмена/останов прогона (Stop/Cancel) без зависания UI.
5) Telegram выключен → никаких попыток отправки.
6) Telegram включен, но не настроен → предупреждение, прогон не падает.

---

## 2. Испытания по модулям (минимальный набор)
### A1 UiScenario
- Сценарий: Navigate → WaitForSelector → Click → AssertText.
- Ожидаемо: Success для всех шагов; при ошибке — скриншот (OnError).

### A2 UiSnapshot
- Сценарий: открыть список URL и снять скриншоты.
- Ожидаемо: папка screenshots заполнена; report содержит ссылки.

### A3 UiTiming
- Сценарий: измерить время загрузки страницы 10 итераций.
- Ожидаемо: метрики avg/p95/p99 заполнены.

### B1 HttpFunctional
- Сценарий: GET/HEAD endpoint, ожидаемый статус 200.
- Негатив: endpoint возвращает 500 → фиксируется failure.

### B2 HttpPerformance
- Iterations: 30; Parallelism: 3.
- Ожидаемо: метрики latency и суммарная длительность.

### B3 HttpAssets
- Сценарий: список статических ресурсов (css/js/img) и проверка 200.

### C1 NetDiagnostics
- Сценарий: DNS ok, TCP ok, TLS ok.
- Негатив: неверный hostname → DNS fail.

### C2 Availability
- Сценарий: проверка доступности (ping/HTTP) 10 итераций.

### C3 SecurityBaseline
- Сценарий: наличие HTTPS редиректа, проверка security headers.

### C4 Preflight
- Сценарий: запуск любого тяжёлого модуля с включенным Preflight.
- Ожидаемо: preflight запускается первым и пишет результаты.

---

## 3. Демонстрационные кейсы для защиты ВКР
1) «Не открывается из сети» → C1/C2 локализуют уровень (DNS/TCP/TLS).
2) «TLS сертификат истёк» → C1 показывает причину.
3) «Медленно грузится UI» → A3 фиксирует метрики.
4) «API отдаёт 500» → B1 фиксирует 5xx и детали.

---

## 4. Артефакты для показа
- `report.json` (всегда)
- `report.html` (если включено)
- `logs/run.log`
- `screenshots/*` (UI модули и/или OnError)

---

## Связанные файлы / входы / выходы
- Входы: критерии приёмки из `01_CANON.md`.
- Выходы: чек-листы для `07_TRACEABILITY.md` и демонстраций.


## 5. Smoke (быстрые честные прогоны для 10 модулей)
1) **A1 ui.scenario**: `TargetUrl=https://example.com`, шаги `body click` + `h1 click/fill` → ожидаемо: run в SQLite, `report.json`, при ошибке шага `screenshots/step_*.png`.
2) **A2 ui.snapshot**: один URL `https://example.com` → ожидаемо: `screenshots/snapshot_*.png`, запись хеша/результата в `report.json`.
3) **A3 ui.timing**: один URL `https://example.com`, 5 итераций через профиль → ожидаемо: набор `TimingResult` в `report.json`.
4) **B1 http.functional**: GET `https://example.com` ожидание `200` → ожидаемо: Success check в отчёте.
5) **B2 http.performance**: URL `https://example.com`, профиль `Duration=10s`, `Parallelism=5` → ожидаемо: серия latency-check результатов в `report.json`.
6) **B3 http.assets**: список из 1–3 URL ассетов (css/js/image) → ожидаемо: статусы и latency для каждого ассета.
7) **C1 net.diagnostics**: host `example.com`, port `443` → ожидаемо: DNS/TCP/TLS результаты с деталями.
8) **C2 net.availability**: target `https://example.com`, профиль `Iterations=10` → ожидаемо: 10 последовательных проверок и итог uptime в логе.
9) **C3 net.security**: URL `https://example.com` → ожидаемо: проверки security headers + TLS/redirect в `report.json`.
10) **C4 net.preflight**: запуск preflight с пустой/дефолтной целью → ожидаемо: проверки FS/SQLite/Chromium, Completed без падения.

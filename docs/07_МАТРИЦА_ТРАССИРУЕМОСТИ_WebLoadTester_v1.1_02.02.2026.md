
# Матрица трассируемости — WebLoadTester

**Версия:** v1.2 08.03.2026

## 0. Как читать матрицу
- FR = функциональное требование
- NFR = нефункциональное требование
- CODE = ключевые компоненты кода
- TEST = пункт из `06_ПЛАН_ИСПЫТАНИЙ...`

---

## 1. Основная матрица (MVP)

| ID | Требование | Модули/разделы | CODE (ключевые файлы) | TEST |
|---|---|---|---|---|
| FR-01 | RunId для каждого прогона + папка runs/<runId>/ | Все | `MainWindowViewModel`, `ArtifactStore`, `RunOrchestrator` | 1.1 |
| FR-02 | report.json формируется всегда | Все | `JsonReportWriter`, `ArtifactStore.SaveJsonReportAsync` | 1.2 |
| FR-03 | HTML отчёт опционально | Все | `HtmlReportWriter`, флаг `RunProfile.HtmlReportEnabled` | 1.2 |
| FR-04 | Telegram опционально, ошибки не влияют | Все | `TelegramPolicy`, `SendTelegramAsync` | 1.5–1.6 |
| FR-05 | Хранение тестов/версий в SQLite | Все | `SqliteRunStore` таблицы `TestCases/TestCaseVersions` | 6.1–6.2 |
| FR-06 | Хранение истории прогонов в SQLite | Все | `SqliteRunStore` таблицы `TestRuns/TestRunItems/Artifacts` | 1.3 |
| FR-07 | 4 вкладки верхнего уровня | UI | `MainWindow.axaml`, `MainWindowViewModel` | Smoke |
| FR-08 | Режимы нагрузки ядра: Iterations/Duration | Все | `RunProfile.Mode`, `RunOrchestrator` | 2.x |
| NFR-01 | Кроссплатформенность Windows/Linux | Все | .NET 8 + Avalonia | Smoke |
| NFR-02 | UI без наложений + прокрутка | UI | XAML + styles | Smoke |

---

## 2. Трассировка по модулям (упрощённо)

| Модуль | Назначение | Ключевой выход | TEST |
|---|---|---|---|
| UiScenario | действия+assertions | screenshots/log/report | 2.1 |
| UiSnapshot | визуальные снимки | screenshots/report | 2.2 |
| UiTiming | тайминги UI | метрики avg/p95/p99 | 2.3 |
| HttpFunctional | функциональные HTTP проверки | statusCode/error | 2.4 |
| HttpPerformance | latency метрики | avg/p95/p99 | 2.5 |
| HttpAssets | проверки ассетов | status/size | 2.6 |
| NetDiagnostics | DNS/TCP/TLS | probe details | 2.7 |
| Availability | доступность | OK/FAIL | 2.8 |
| SecurityBaseline | baseline security | checklist | 2.9 |
| Preflight | готовность | Ready/Not Ready | 2.10 |

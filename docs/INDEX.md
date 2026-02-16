# WebLoadTester — UI спецификация и полное описание ПО (единый исходный документ)

**Версия:** v3.4 16.02.2026
**Актуально для:** `WebLoadTester 16.02.2026 6pm.tar` и решений из `history_all_chats_v2 16.02.2026.md`.

Этот файл — **единственный источник правды (TO‑BE)** для разработки WebLoadTester: по нему можно реализовать продукт с нуля (архитектура, данные, UI/UX, модули, запуск, отчёты, ограничения, правила). Код текущей версии считается AS‑IS и приводится к TO‑BE через раздел **«Дельта внедрения»**.

---

## 0. Инварианты продукта

### 0.1. Состав и назначение

* **10 модулей**, сгруппированы в **3 семейства** и отражены в UI как 4 вкладки верхнего уровня:

  1. **UI тестирование** (Playwright/Chromium)
  2. **HTTP тестирование**
  3. **Сеть и безопасность** (только безопасные проверки, без атак)
  4. **Прогоны** (история и артефакты)
* Продукт — **desktop MVP** (без распределённых агентов/кластера).
* UI — **на русском** (заголовки таблиц, подсказки, ошибки). Технические термины допустимы только в help/tooltip.

### 0.2. Единая модель запуска (для всех модулей)

* Запуск управляется **профилем запуска (RunProfile)**:

  * режим: **Iterations** (N итераций) или **Duration** (T секунд)
  * **Parallelism (X)** — число параллельных воркеров
  * **TimeoutSeconds** — таймаут одной итерации
  * **PauseBetweenIterationsMs** — пауза между итерациями (пейсинг)
  * флаги: HTML‑отчёт, скриншоты, Telegram
* Для UI‑модулей: `Parallelism = X` означает **X одновременных контекстов/окон**.
* **Stop ≠ Cancel**:

  * **Стоп** — мягко: завершить текущую итерацию воркера и не начинать новую.
  * **Отмена** — жёстко: немедленная отмена через `CancellationToken`.

### 0.3. Хранилище и артефакты

* Данные: **SQLite**.
* Результаты: файловая структура **`runs/{RunId}/`**.
* Отчёты: **`report.json` всегда**, `report.html` — опционально.

### 0.4. Безопасность

* Запрещены атакующие функции: скан уязвимостей, bruteforce, эксплуатация, агрессивные стресс‑атаки.

---

## 1. Технологии и зависимости

* .NET 8
* Avalonia 11 (FluentTheme), тема фиксируется в Light (`RequestedThemeVariant = Light`)
* MVVM (CommunityToolkit.Mvvm)
* Playwright for .NET (Chromium) — для UI семейства
* SQLite (Microsoft.Data.Sqlite)

### 1.1. Playwright/Chromium (обязательное UX‑правило)

Если Chromium не установлен — **показываем баннер** и кнопку **«Установить Chromium»** на **всех UI‑модулях**.

* Установка из UI кладёт браузеры рядом с приложением в `./browsers`.
* CLI (справка):

  * Windows: `pwsh ./playwright.ps1 install chromium`
  * Linux: `./playwright.sh install chromium`

---

## 2. Архитектура и слои

### 2.1. Слои

* `Presentation/` — Views, ViewModels, Styles (без бизнес‑логики)
* `Modules/` — 10 модулей, каждый реализует единый контракт
* `Core/` — оркестратор, доменные сущности, отчётность, контракты
* `Infrastructure/` — SQLite, settings.json, Telegram, артефакты, Playwright factory, HTTP/network реализации

### 2.2. Контракты

**Модуль** реализует интерфейс вида `ITestModule`:

* `Id`, `DisplayName`, `Family`, `SettingsType`
* `CreateDefaultSettings()`
* `Validate(settings)`
* `ExecuteAsync(settings, RunContext ctx, CancellationToken ct)`

**Оркестратор (RunOrchestrator)** обязан:

* валидировать профиль + настройки модуля
* (опц.) выполнить Preflight
* запускать `Parallelism` воркеров по Iterations/Duration
* уважать Stop/Cancel
* собирать `RunItem` и артефакты
* сохранять итоги в SQLite + `runs/{RunId}/`

**RunContext (TO‑BE)** включает минимум:

* `RunId`
* `WorkerId`
* `Iteration`
* `RunDirectory`, `Artifacts` API
* доступ к сервисам: HTTP client factory, Playwright factory, DNS/TCP/TLS, Logger

---

## 3. Хранение: пути, settings.json, SQLite

### 3.1. Директории

По умолчанию (LocalAppData):

* `DataDirectory = .../WebLoadTester/data`
* `RunsDirectory = .../WebLoadTester/runs`
* `DatabasePath = {DataDirectory}/webloadtester.db`

Структура артефактов прогона (TO‑BE):

* `runs/{RunId}/report.json`
* `runs/{RunId}/report.html` (если включено)
* `runs/{RunId}/logs/run.log`
* `runs/{RunId}/screenshots/w{WorkerId}/...`
* `runs/{RunId}/profiles/w{WorkerId}/...`

### 3.2. settings.json (персистентные настройки)

Файл: `LocalAppData/WebLoadTester/settings.json`

* `DataDirectory`
* `RunsDirectory`
* `Telegram`:

  * `Enabled`
  * `BotToken`
  * `ChatId`
  * `NotifyOnStart`, `NotifyOnFinish`, `NotifyOnError`
  * `ProgressMode` (Off/EveryN/EveryTSeconds)
  * `ProgressEveryN`
  * `RateLimitSeconds`

### 3.3. SQLite (логическая схема)

Таблицы:

* **TestCases**: библиотека конфигураций
* **TestCaseVersions**: версии конфигураций (PayloadJson)
* **RunProfiles**: опциональные шаблоны профиля (можно без UI‑CRUD в MVP)
* **TestRuns**: прогоны (status, timestamps, snapshot)
* **RunItems**: элементы результатов
* **Artifacts**: реестр артефактов
* **TelegramNotifications**: журнал отправок (по желанию)

Правило: любые изменения схемы сначала фиксируются в этом документе.

---

## 4. Конфигурации (TestCase) и профиль запуска (RunProfile)

### 4.1. Что такое конфигурация

Конфигурация = (настройки модуля) + (профиль запуска).

* В БД хранится версионно: `TestCaseVersions.PayloadJson`.
* При запуске создаётся snapshot профиля в `TestRuns.ProfileSnapshotJson` и/или в `runs/{RunId}/`.

### 4.2. Имена

* Пользователь вводит **Имя (без пробелов)** → `UserName`.
* Итоговое имя: `FinalName = UserName + "_" + ModuleSuffix` (read‑only).

### 4.3. CRUD конфигов (упрощённый и единый)

UI всегда показывает:

* ComboBox `Конфигурация`
* Кнопки: **`Загрузить` / `Сохранить` / `Удалить`**
* Поля: `Имя`, `Итоговое имя`, `Описание`

Правила:

* `Сохранить` всегда создаёт новую версию (если конфиг существует).
* `Старт` выполняет: валидировать → автосохранить → запустить.
* `Удалить` удаляет библиотеку конфигурации, **прогоны не трогаем**.

### 4.4. RunProfile (поля и дефолты)

Обязательные поля:

* `Mode`: Iterations/Duration
* `Iterations` (если Iterations) > 0
* `DurationSeconds` (если Duration) > 0
* `Parallelism` 1..25
* `TimeoutSeconds` > 0
* `PauseBetweenIterationsMs` >= 0

Рекомендуемое решение по лимиту Duration:

* поднять max с 60 до **600** секунд и показывать предупреждение при >60.

---

## 5. Оркестратор: точная семантика запуска

### 5.1. Порядок операций (обязательный)

1. Validate RunProfile
2. Validate module settings
3. Save config version (если требуется автосейв)
4. Create TestRun(Status=Running)
5. Prepare run folder (`runs/{RunId}`)
6. Preflight (опционально по флагу)
7. Execute: воркеры + сбор результатов
8. Save `RunItems`, `Artifacts`, `report.json` (+ `report.html`)
9. Finalize status + timestamps

### 5.2. Воркеры

* Запускаем `Parallelism` воркеров: `WorkerId = 1..X`.
* Итерации:

  * Iterations: распределяем N по воркерам (round‑robin или через общий счётчик).
  * Duration: выполняем итерации до дедлайна.
* Между итерациями: `Delay(PauseBetweenIterationsMs)`.
* Каждая итерация ограничена `TimeoutSeconds`.

### 5.3. Статусы

Рекомендуемые статусы прогона:

* `Running`
* `Success`
* `Failed`
* `Canceled`
* `Stopped` (мягкая остановка)

Правила итогового статуса:

* если была отмена → `Canceled`
* если был Stop → `Stopped` (если не было Failure)
* если есть хотя бы один Failure (и не Cancel) → `Failed`
* иначе → `Success`

---

## 6. UI/UX канон

### 6.1. Главное окно

* Верхняя статус‑панель: состояние БД, Telegram, Playwright + кнопка **Настройки**.
* Вкладки: UI / HTTP / Сеть и безопасность / Прогоны.
* Лог‑дровер (Expander) с ограничением ~500 строк, кнопки: очистка/копировать, автоскролл.

### 6.2. ModuleWorkspace (унифицированная компоновка)

На каждой вкладке‑модуле одинаковый каркас:

1. **Карточка «Конфигурация»** (CRUD)
2. **Карточка «Настройки модуля»**
3. **Карточка «Профиль запуска»**
4. **Панель запуска**: Старт / Стоп / Отмена + прогресс + кнопки открыть JSON/HTML/папку

Правила:

* Один ScrollViewer на вкладку (без вложенных).
* Ровная сетка `field-row`: label слева, control справа, одинаковые отступы.
* Никаких наложений текста: корректные `Grid.ColumnDefinitions`, `TextTrimming`, min widths.

### 6.3. SettingsWindow

Разделы:

* Пути: DataDirectory / RunsDirectory / DatabasePath
* Telegram: поля из §3.2 + кнопка **«Тестовое сообщение»**

### 6.4. Вкладка «Прогоны»

Обязательные функции:

* таблица прогонов + фильтры
* открыть папку прогона
* открыть report.json / report.html
* повторить запуск (подтянуть конфиг+профиль)
* удалить прогон (БД + папка `runs/{RunId}`) с подтверждением
* копировать RunId/путь

---

## 7. Каталог модулей (10)

| Id               | UI‑имя                     | Семейство | Назначение                    |
| ---------------- | -------------------------- | --------- | ----------------------------- |
| ui.scenario      | A1 UI сценарий             | UI        | шаги в браузере               |
| ui.snapshot      | A2 UI снимки               | UI        | скриншоты URL/элементов       |
| ui.timing        | A3 UI тайминги             | UI        | метрики загрузки              |
| http.functional  | B1 HTTP функциональные     | HTTP      | проверки условий              |
| http.performance | B2 HTTP производительность | HTTP      | агрегаты latency/ошибок       |
| http.assets      | B3 HTTP ассеты             | HTTP      | проверка статических ресурсов |
| net.diagnostics  | C1 DNS/TCP/TLS             | NetSec    | безопасная диагностика        |
| net.availability | C2 Доступность             | NetSec    | атомарная проверка            |
| net.security     | C3 Security baseline       | NetSec    | заголовки/redirect            |
| net.preflight    | C4 Preflight               | NetSec    | быстрый предстартовый чек     |

---

## 8. Модули: действия пользователя, обязательные поля, результат

Ниже описано, что пользователь делает в UI, какие поля обязательны и какие артефакты получает.

### 8.A. UI тестирование (Playwright)

#### A1. ui.scenario — UI сценарий

Пользователь:

1. (Опционально) выбирает конфигурацию → **Загрузить**.
2. Заполняет:

   * `Стартовый URL` (если первый шаг не «Переход»)
   * `Таймаут ожиданий, мс` > 0
   * `Шаги` (минимум 1)
3. Настраивает профиль запуска:

   * `Parallelism = X` (число окон/контекстов)
   * режим Iterations или Duration
   * `PauseBetweenIterationsMs` для «раз в N минут»
   * политика скриншотов (Off/OnError/Always)
4. Нажимает **Старт**.

Таблица шагов (Variant B): `Действие + Селектор + Значение + Задержка, мс`.

* Переход: значение = URL (обязательно)
* Ожидание элемента: селектор (обязательно)
* Клик: селектор (обязательно)
* Ввод текста: селектор + значение (обязательно)
* Проверка текста: селектор + ожидаемый текст (обязательно)
* Скриншот: селектор опционально
* Пауза: задержка (обязательно)

Результат:

* `report.json` (+ `report.html` если включено)
* `runs/{RunId}/screenshots/w{WorkerId}/...` (по политике)
* `runs/{RunId}/logs/run.log`
* RunItems: по одному на шаг (status, duration, error, path screenshot)

#### A2. ui.snapshot — UI снимки

Пользователь:

1. Заполняет таблицу `Цели` (минимум 1): `URL` обязателен, `Селектор/Имя` опционально.
2. Выбирает `WaitUntil`, `TimeoutSeconds`, `FullPage`, viewport (оба поля или ни одного).
3. Настраивает профиль запуска (Parallelism, Iterations/Duration, PauseBetweenIterationsMs).
4. Старт.

Результат: скриншоты + RunItems по целям.

#### A3. ui.timing — UI тайминги

Пользователь:

1. Заполняет `Цели` (минимум 1): `URL` обязателен.
2. Выбирает `WaitUntil`, `TimeoutSeconds`.
3. Старт.

Результат: RunItems с timing‑метриками.

### 8.B. HTTP тестирование

#### B1. http.functional — функциональные проверки

Пользователь:

1. Заполняет `BaseUrl` (обяз.), `TimeoutSeconds` (обяз.).
2. В списке endpoints добавляет минимум 1 endpoint: `Имя`, `Метод`, `Путь`.
3. В details задаёт проверки: `ExpectedStatusCode` (обяз.), опционально latency/headers/body.
4. Старт.

Результат: RunItems по endpoints (код/latency/нарушения условий).

#### B2. http.performance — производительность (упрощённо)

Пользователь:

1. Заполняет BaseUrl/Timeout.
2. Список endpoints.
3. Настраивает профиль запуска (Parallelism + Iterations/Duration).
4. Старт.

Результат: агрегаты latency/ошибок по endpoints + общий summary.

#### B3. http.assets — ассеты

Пользователь:

1. Заполняет `Assets` (минимум 1): URL обязателен.
2. Опциональные условия: content‑type, max latency, max size.
3. Старт.

Результат: RunItems по assets.

### 8.C. Сеть и безопасность

#### C1. net.diagnostics — DNS/TCP/TLS

Пользователь:

1. Заполняет Hostname (обяз.).
2. Выбирает AutoPortsByScheme или задаёт Ports (если Auto выключен).
3. Выбирает флаги DNS/TCP/TLS.
4. Старт.

Результат: RunItems DNS/TCP/TLS с деталями.

#### C2. net.availability — доступность

Пользователь:

1. Выбирает тип: Http или Tcp.
2. Заполняет цель: URL или `host:port`.
3. TimeoutMs.
4. Для повторов использует профиль запуска (Duration + PauseBetweenIterationsMs).
5. Старт.

Результат: RunItems по каждой итерации проверки.

#### C3. net.security — security baseline

Пользователь:

1. URL (обяз.).
2. Выбирает проверки заголовков/redirect.
3. Старт.

Результат: RunItems по каждой проверке + пояснения.

#### C4. net.preflight — preflight

Пользователь:

1. Target (опц.)
2. Выбирает проверки DNS/TCP/TLS/HTTP.
3. Старт.

Правило: если preflight включён как стадия — при FAIL основной запуск не начинается.

---

## 9. Отчётность

### 9.1. report.json (обязательный контракт)

Минимальная структура:

* `runId`, `moduleId`, `finalName`
* `startedAtUtc`, `finishedAtUtc`, `status`
* `profile` (snapshot)
* `summary` (totals, failures, durationMs)
* `items[]`: массив результатов (соответствует RunItems)
* `artifacts[]`: пути/типы (соответствует Artifacts)

Правила:

* `report.json` пишется даже при Failed/Canceled/Stopped.
* В `items[].extraJson` допускается произвольная структура (например workerId/iteration, метрики).

### 9.2. report.html

* генерируется из тех же данных
* скриншоты показываются ссылками + превью

---

## 10. Тест‑план (минимальный smoke)

1. net.preflight → Success
2. net.diagnostics → Success
3. http.functional (1 endpoint) → Success
4. ui.snapshot (1 URL) → скрин создан
5. Прогоны: открыть JSON/папку, повторить
6. Telegram: тестовое сообщение + уведомление о завершении

---

## 11. НФТ (нефункциональные требования)

* Надёжность: валидный report.json при любом исходе.
* Воспроизводимость: повтор прогона из истории восстанавливает конфиг+профиль.
* Производительность UI: лог ограничен, список виртуализирован.
* Портируемость: Windows/Linux, без хардкодов путей.
* Юзабилити: единый layout, без наложений, русские подписи.

---

## 12. Приложение A — требования к оформлению ПЗ (из корпоративного стандарта)

Минимум, который обязателен для ВКР:

* Структура: титульный, реферат (1–2 стр), содержание, введение (без нумерации), основная часть, заключение, источники, приложения.
* Формат A4.
* Поля: слева 20 мм, справа 10 мм, сверху 20 мм, снизу 20 мм.
* Абзац 10 мм, интервал 1.5.
* Шрифт Times New Roman или Arial, 12–14.
* Нумерация страниц арабскими цифрами, номер не ставится на титуле.

---

## 13. Дельта внедрения (что привести к TO‑BE в коде)

1. Добавить `PauseBetweenIterationsMs` во все слои и в snapshot.
2. Реализовать модель `Parallelism = N workers` + `WorkerId/Iteration` в контексте.
3. Реализовать Stop (soft) отдельно от Cancel (hard).
4. Привести UI CRUD конфигов к `Загрузить/Сохранить/Удалить` (убрать «Перезаписать»).
5. UI‑артефакты по воркерам: `profiles/w{WorkerId}`, `screenshots/w{WorkerId}`.
6. `net.availability`: убрать legacy IntervalSeconds и циклы.
7. Telegram: персистентность settings.json + тестовое сообщение.
8. `http.functional`: list+details (устранить ошибки биндинга/MC3073).
9. Playwright баннер: показывать для всех UI‑модулей.
10. Прогоны: удалить прогон = БД + папка `runs/{RunId}` (с подтверждением).

---

## 14. Глоссарий

* Модуль — функциональный блок, запускаемый оркестратором.
* Семейство — группа модулей (UI/HTTP/NetSec).
* Конфигурация — сохраняемый набор настроек модуля + профиля.
* Версия конфигурации — snapshot JSON на момент сохранения.
* Прогон — запуск конкретной версии конфигурации.
* RunItem — единый элемент результата.
* Артефакт — файл результата (json/html/log/screenshot).
* Worker — параллельный исполнитель.
* Preflight — предстартовая проверка.

**Конец документа.**

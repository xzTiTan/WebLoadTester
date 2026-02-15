# WebLoadTester

**Версия документации:** v2.9 15.02.2026

WebLoadTester — кроссплатформенное настольное приложение на **.NET 8 + Avalonia 11** для эксплуатационной диагностики и тестирования **UI / HTTP / сети**.

Ключевая идея: любой запуск формирует **RunId** и воспроизводимые доказательства (SQLite-запись + папка артефактов), чтобы быстро отвечать на вопрос «где именно проблема: DNS/TCP/TLS/HTTP/UI».

## Инварианты (не менять без правки Канона)
- **4 вкладки верхнего уровня:** UI тестирование / HTTP тестирование / Сеть и безопасность / Прогоны.
- **10 модулей (3 семейства):**
  - A1 UI сценарий, A2 UI снимки, A3 UI тайминги
  - B1 HTTP функциональные, B2 HTTP производительность, B3 HTTP ассеты
  - C1 DNS/TCP/TLS диагностика, C2 монитор доступности, C3 security baseline (без атак), C4 preflight
- **SQLite** — обязательное хранилище (кроссплатформенно Windows/Linux).
- **Артефакты прогона:** папка `runs/{RunId}/` + `report.json` всегда, `report.html` — по флагу профиля.
- **Telegram** — опционально; ошибки Telegram не ломают прогон.
- **Никаких атакующих/опасных функций** (flood/packet attack/эксплуатация уязвимостей и т.п.).

## Документация (актуальный набор)

1) [docs/00_INDEX.md](docs/00_INDEX.md) — индекс и рекомендуемый порядок чтения.
2) [docs/01_CANON.md](docs/01_CANON.md) — требования, границы, критерии приёмки (источник истины).
3) [docs/02_ARCHITECTURE_AND_DATA.md](docs/02_ARCHITECTURE_AND_DATA.md) — архитектура, потоки, SQLite, артефакты, карта кода.
4) [docs/03_UI_CANON.md](docs/03_UI_CANON.md) — UI канон и поведения (4 вкладки, pinned-rows, log drawer, UX-правила).
5) [docs/04_GLOSSARY.md](docs/04_GLOSSARY.md) — термины (единые определения для кода/ВКР/UI).
6) [docs/05_TEST_PLAN_AND_TRACEABILITY.md](docs/05_TEST_PLAN_AND_TRACEABILITY.md) — тест-план + трассируемость.
7) [docs/06_CODEX_RULES_AND_CHANGELOG.md](docs/06_CODEX_RULES_AND_CHANGELOG.md) — guardrails Codex + журнал изменений.
8) [docs/07_VKR_DRAFT.md](docs/07_VKR_DRAFT.md) — каркас пояснительной записки (ВКР) и привязка к артефактам.
9) [docs/08_FULL_SOFTWARE_DESCRIPTION.md](docs/08_FULL_SOFTWARE_DESCRIPTION.md) — цельное описание ПО для обзора/защиты.

## Быстрый старт (dev)
```bash
dotnet restore
dotnet build
dotnet run
```

### Playwright/Chromium (для UI модулей A1–A3)
Приложение ожидает браузеры в `AppContext.BaseDirectory/browsers`.

После сборки установите браузеры рядом с output-папкой:
- **Windows (PowerShell):**
  ```powershell
  $env:PLAYWRIGHT_BROWSERS_PATH = (Join-Path (Get-Location) "bin/Debug/net8.0/browsers")
  .\bin\Debug\net8.0\playwright.ps1 install
  ```
- **Linux/macOS (bash):**
  ```bash
  PLAYWRIGHT_BROWSERS_PATH=bin/Debug/net8.0/browsers \
    ./bin/Debug/net8.0/playwright.sh install
  ```

## Где хранятся данные
По умолчанию (через `AppSettingsService`) используется `%LocalAppData%/WebLoadTester/`:
- `data/` — данные и SQLite (`webloadtester.db`)
- `runs/` — артефакты прогонов
- `settings.json` — пути хранения

Структура артефактов:
- `runs/{RunId}/report.json` — всегда
- `runs/{RunId}/report.html` — если включено в профиле
- `runs/{RunId}/logs/run.log`
- `runs/{RunId}/screenshots/*` (UI модули)

## Ограничения и законность
Инструмент предназначен **только** для проверок систем, которыми вы владеете или на которые получили явное разрешение. C3 — только baseline-проверки без атак.

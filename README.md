# WebLoadTester

Небольшое десктопное приложение (.NET + Avalonia) для UI-нагрузки через Playwright. Поддерживает несколько режимов: E2E, Load, Stress, Endurance и Screenshot.

## Быстрый старт
1. Убедитесь, что установлен .NET 9 SDK.
2. Перед первым запуском скачайте браузеры Playwright в локальную папку `./browsers`:
   ```bash
   pwsh ./bin/Debug/net9.0/playwright.ps1 install chromium
   ```
   Если нужен альтернативный CDN, задайте переменную окружения `PLAYWRIGHT_DOWNLOAD_HOST` перед установкой. Во время работы приложение использует `PLAYWRIGHT_BROWSERS_PATH` ссылающийся на `./browsers`.
3. Соберите и запустите приложение:
   ```bash
   dotnet build
   dotnet run
   ```
4. Заполните настройки, выберите `Вид тестирования` и нажмите «Начать».

## Режимы тестирования
- **E2E** – последовательные проверки сценария.
- **Load** – параллельные прогоны с фиксированной параллельностью.
- **Stress** – ступенчатый рост нагрузки (RampStep / RampDelaySeconds / RunsPerLevel).
- **Endurance** – выполнение сценария заданное время с постоянной параллельностью.
- **Screenshot** – быстрые проверки доступности/визуальных изменений, основной результат – скриншот.

## Отчёты и артефакты
- После каждого запуска сохраняется JSON в `reports/report_yyyy-MM-dd_HH-mm-ss.json` (summary + список прогонов).
- Скриншоты пишутся в `screenshots/`.
- Телеграм-настройки находятся в правой панели; отправка сообщений/скриншотов использует введённые токен и ChatId.

## Пример структуры отчёта
```json
{
  "meta": { "startedAt": "2024-01-01T12:00:00Z", "finishedAt": "2024-01-01T12:05:00Z" },
  "testType": "Load",
  "settings": { "targetUrl": "https://example.com", "totalRuns": 20, "concurrency": 5 },
  "summary": { "totalRuns": 20, "ok": 18, "fail": 2, "avgDurationMs": 1234.5 },
  "runs": [ { "workerId": 1, "runId": 1, "success": true, "duration": "00:00:01.23" } ]
}
```

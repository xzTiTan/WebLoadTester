
# Правила Codex и журнал изменений — WebLoadTester

**Версия:** v2.4 11.02.2026

**Назначение:** описывает guardrails, типовые ошибки и журнал изменений документации.  
**См. также:** [00_INDEX.md](00_INDEX.md), [01_CANON.md](01_CANON.md), [02_TECH_SPEC.md](02_TECH_SPEC.md), [03_UI_SPEC.md](03_UI_SPEC.md).

## 1. Режим работы (стандарт)
1) Любое изменение начинается с чтения актуальных якорных файлов (01–04 и, при необходимости, 07).
2) В PR запрещено:
   - менять базис (3 семейства/10 модулей/4 вкладки; iterations/duration; Telegram опционально) без изменения Канона.
   - ломать структуру `runs/<runId>/` и обязательность `report.json`.

## 2. Формат задач для Codex (шаблон)
В промпте всегда указывай:
- цель изменения,
- затрагиваемые файлы,
- критерии готовности,
- ограничения (не добавлять stress/soak, Telegram опционально и т.п.).

Мини-шаблон:
- **Context:** мы работаем по якорным файлам 01–04.
- **Goal:** …
- **Do:** …
- **Do not:** …
- **Acceptance:** сборка, запуск, сценарии smoke.

## 3. Политика веток/PR
- Ветка: `codex/<topic>-<short>`
- PR summary: что сделано, что не сделано, тестирование.

## 4. Версионирование якорных файлов
- Любая правка якорного файла → поднять версию и дату в шапке файла.
- Добавить запись в §8 «Журнал изменений».

## 5. Типовые ошибки и причины (Avalonia + Codex)
- **XAML/WPF паттерны в Avalonia**: `SetterTargetType` или `Theme Mode` приводят к AVLN/MC ошибкам компиляции (Avalonia не принимает WPF-специфику).
- **ElementName с кавычками**: `ElementName='\"Root\"'` → MC3102/AVLN2000 (некорректный парсер XAML).
- **`object?` DataContext + CompiledBinding**: ошибки `MC3073/AVLN2000`, когда `x:DataType` не задан или неверно предполагается тип контекста.
- **DataTemplate контекст Item vs VM**: «DataContext не существует на ArtifactListItem» — нужно определить `x:DataType` для шаблона и использовать правильный контекст.
- **Duplicate assembly attributes**: CS0579 при ручном дублировании `TargetFrameworkAttribute` или Attributes в `AssemblyInfo.cs`.
- **Test leakage**: `xUnit`/`Test` типы в основном проекте → зависимости тестов протекают в приложение.

## 6. Guardrails Block (вставка в каждый промпт Codex)
```text
TODAY = 03.02.2026 (Europe/Berlin). НЕ ставь будущие даты.

Запрещено в Avalonia/XAML:
- SetterTargetType (WPF-паттерн).
- Theme Mode (атрибут `Mode` только в `FluentTheme/Styles`).
- ElementName='\"...' (кавычки внутри значения).
- WrapPanel Spacing (если не поддерживается версией Avalonia).
- Ручные TargetFrameworkAttribute в исходниках.

Требования к XAML:
- При CompiledBinding всегда указывать x:DataType на UserControl/DataTemplate.
- Доступ к VM только через RelativeSource / x:Reference (не через произвольный ElementName).

Документация:
- Не обновлять версии/даты без явной задачи.
- При правке якорных файлов — обновить версию/дату и changelog.

Финальная проверка:
- grep запрещённых токенов (SetterTargetType, ElementName='\"', Mode=, WrapPanel.*Spacing, TargetFrameworkAttribute).
```

## 7. Checklist перед финалом
- grep запрещённых токенов в XAML/AXAML/CSProj (см. Guardrails).
- `dotnet build` (Release).
- `dotnet test` (если есть тесты).
- проверить XAML compile (ошибки AVLN/MC).
- проверить, что ссылки на docs не битые.

## 8. Журнал изменений
### v2.4 11.02.2026
- Исправлено падение `NullReferenceException` в `ModuleConfigViewModel.SaveNewAsync`: `RunProfile` теперь инициализируется до создания `ModuleItemViewModel` в `MainWindowViewModel`.
- В `ModuleConfigViewModel` добавлены `ArgumentNullException.ThrowIfNull(...)` для обязательных зависимостей и безопасная обработка ошибок сохранения конфигурации в `SaveNewAsync`.
- В `MainWindowViewModel.StartAsync` добавлена предварительная валидация через `_orchestrator.Validate(...)` до старта прогона, с понятным статусом и предупреждением в логах.

### v2.3 11.02.2026
- Исправлена ошибка сборки CS0535: из `IArtifactStore` удалено устаревшее свойство `ProfilesRoot`, которое больше не используется после упрощения MVP-хранилища профилей (профили остаются в SQLite).

### v2.2 11.02.2026
- Добавлен единый каталог модулей `ModuleCatalog` с `ModuleSuffix`; превью/сохранение конфигов теперь формируют имя строго как `UserName_ModuleSuffix`.
- В секции «Параметры запуска» поля `Headless` и `ScreenshotsPolicy` показываются только для UI-модулей (A1–A3).
- Стабилизирован compiled binding в `HttpPerformanceSettingsView`: список HTTP-методов переведён на `x:Static HttpPerformanceEndpoint.MethodOptions`.

### v2.1 11.02.2026
- CI lint в `.github/workflows/ci.yml` сделан точечным: вместо общего поиска `Mode=` теперь проверяется только `Mode` в теге `FluentTheme`, чтобы не ловить валидные `Binding Mode=TwoWay/OneWay`.
- Удалён `ProfilesDirectory` из настроек приложения и `ArtifactStore`: профили запусков остаются в SQLite (`RunProfiles`), в окне настроек оставлены только пути для данных/прогонов/БД.

### v2.0 10.02.2026
- Модули C1/C2/C3/C4 приведены к честному MVP-циклу: добавлены явные progress/log события, обработка ошибок как результатов и без зависаний UI.
- `net.availability` выполняет ровно один probe на вызов `ExecuteAsync` (без внутренней задержки/цикла), интервал оставлен на уровне оркестратора профиля.
- Playwright-модули `ui.snapshot` и `ui.timing` теперь отдают пользователю явную причину отсутствия Chromium с путём установки, а не расплывчатую ошибку.
- HTTP-модули (`functional/performance/assets`) получили базовые лог-сообщения запуска для наблюдаемости даже при закрытой панели логов.

### v1.9 10.02.2026
- Исправлен `ui.scenario`: добавлены нормализация URL, ограниченные таймауты навигации/шагов, ранний прогресс и детальные логи действий/ошибок.
- Выполнение шагов приведено к канону MVP: при непустом `Text` выполняется Fill, иначе Click; action в настройках сохраняется для обратной совместимости.
- Добавлен UX-баннер установки Chromium с кнопкой `Install Chromium (Playwright)` и безопасной командой установки через `playwright.sh/.ps1`; ошибки не падают в UI и пишутся в лог.
- Обновлено отображение прогресса в `MainWindowViewModel`: для duration-режима показывается `Итераций: N (duration)`, а после завершения/ошибки/отмены индикатор перестаёт быть бесконечным.

### v1.7 03.02.2026
- Документация переведена на стабильные имена, старые версии перенесены в docs/_archive.
- Добавлены guardrails, типовые ошибки и чек-лист для Codex.
- Добавлены CI-проверки и локальный lint для запрещённых токенов Avalonia.

### v1.6 03.02.2026
- Исправлены биндинги app-bar на MainWindowViewModel через VmProxy (устранены ошибки AVLN2000).
- Секция профиля запуска сделана компактнее: расширенные параметры вынесены в Expander.
- Добавлен фильтр модулей, подсказка по запуску и ограничения высоты для тяжёлых секций.

### v1.5 02.02.2026
- Перенесено управление запуском в закреплённую верхнюю панель ModuleWorkspace и добавлена кнопка перезапуска.
- Перезапуск сделан безопасным: отмена с ожиданием завершения перед новым стартом.
- Добавлены проценты прогресса и индикатор неопределённого прогресса.

### v1.4 02.02.2026
- PROMPT 3/3: реализованы A2/A3/B2/B3/C2/C3, обновлены настройки UI, добавлены проверки безопасности без атак.
- Telegram-уведомления оставлены опциональными, ошибки не влияют на прогон; обновлён журнал.
- Добавлены опциональные проверки и предупреждения нагрузки, скорректированы метрики (p95/p99 только при достаточном N).
- Обновлены docs/04, docs/07 и данный журнал.

### v1.3 02.02.2026
- Исправлены XAML-стили Workspace для корректного Selector/Setter без SetterTargetType.
- Уточнён тестовый проект для корректной сборки xUnit в VS и dotnet test.
- Добавлены версии сборки в csproj (Assembly/File/Informational).

### v1.2 02.02.2026
- Обновлён контракт `ITestModule`: `ExecuteAsync` возвращает `ModuleResult`, итерации/длительность управляет `RunOrchestrator`.
- Добавлены репозитории для тестов/профилей/прогонов/артефактов и расширены SQLite-операции.
- Переведён `ArtifactStore` на возвращение относительных путей артефактов и обновлены JSON/HTML writers.
- Добавлены минимальные тесты для SQLite-инициализации и генерации JSON-отчёта.

### v1.1 02.02.2026
- Сформирован «единый канон» и синхронизированы якорные файлы 01–09 по решениям:
  - Telegram опционально (флаг в профиле), ошибки не влияют на прогон.
  - 4 вкладки верхнего уровня (3 семейства + Прогоны).
  - В ядре только Iterations/Duration; stress/soak вынесены в перспективы.
- Документация приведена в соответствие с фактической структурой кода (Core/Modules/Infrastructure/Presentation).
- Зафиксированы DDL таблиц SQLite и формат отчёта report.json.
- Обновлены README.md, AGENTS.md и добавлен docs/00_INDEX.md как стабильный индекс якорных документов.

### v1.8 10.02.2026
- Удалён неиспользуемый `TestLibraryViewModel`; модульный workspace работает через `ModuleConfigViewModel`.
- Исправлен compiled binding в `HttpFunctionalSettingsView`: список HTTP методов задаётся через `x:Static HttpEndpoint.MethodOptions`.
- `RunOrchestrator` применяет `RunProfile.TimeoutSeconds` как per-iteration timeout через linked CTS, таймаут возвращается как failed result с сообщением `Operation timed out`.
- Нормализованы настройки UI/Net модулей: `UiScenarioSettings` и `UiSnapshotSettings` очищены от дублей run profile, `AvailabilityModule` выполняет одну probe за вызов.
- В `ModuleWorkspaceView` добавлены поля `Headless` и `ScreenshotsPolicy` в секции параметров запуска; метрики переведены на безопасные строковые свойства без `StringFormat`.

# Правила Codex и журнал изменений — WebLoadTester

**Версия:** v2.5 11.02.2026

## 1. Режим работы (обязательный)
1) Перед правками — прочитать `01–03` (и при необходимости `05–07`).
2) Любое изменение: **анализ → план → точечные правки**.
3) Минимально-инвазивно: не переписывать архитектуру «ради красоты».
4) Любая правка docs → поднять версию/дату в шапке и добавить запись в журнал.

## 2. Guardrails против типовых ошибок Avalonia/XAML (обязательный блок)
**Запрещено в Avalonia/XAML:**
- `SetterTargetType` (WPF-паттерн).
- Атрибут `Mode` в `<FluentTheme ... Mode=...>`.
- `ElementName='\"Root\"'` (кавычки внутри значения).
- `WrapPanel Spacing` (если версия Avalonia не поддерживает).
- Ручное добавление `TargetFrameworkAttribute`/дубликатов Assembly attributes.

**Требования к XAML/Bindings:**
- Для CompiledBinding всегда задавать `x:DataType` на `UserControl` и `DataTemplate`.
- Не полагаться на `DataContext` типа `object?`.
- В `DataTemplate` помнить: контекст — элемент коллекции, а не VM окна.

## 3. Guardrails против типовых ошибок SQLite
- В SQL-командах не оставлять параметр со значением `null` (использовать `DBNull.Value`).
- При nullable полях (например, `FinishedAt`) — передавать `DBNull.Value`.

## 4. Проверка готовности (acceptance для PR)
- `dotnet clean`
- `dotnet build -c Release`
- `dotnet test -c Release` (если есть тестовый проект)
- Smoke из `05_TEST_PLAN_AND_TRACEABILITY.md`

## 5. Что делать при конфликте docs
Если обнаружены разночтения:
1) фиксируем, где конфликт (файл+раздел),
2) выбираем приоритет по правилу в `01_CANON.md`,
3) синхронизируем остальные документы и код.

## 6. Журнал изменений
### v2.5 11.02.2026
- Сконсолидированы якорные документы до **8 файлов** (README + 7 docs), убраны дубли и битые ссылки.
- Уточнены guardrails: добавлен отдельный блок по SQLite `DBNull.Value` и актуализированы XAML-запреты.
- Актуализировано описание последних стабилизаций: pre-check перед стартом, защита от NRE в конфиг-VM, фиксы SQLite insert.

### v2.4 11.02.2026
- Ранний pre-check перед стартом: `_orchestrator.Validate(...)` вызывается до `IsRunning=true`, ошибки отображаются в `StatusText` и `StatusMessage`.
- Защита от NRE при сохранении/старте: `RunProfile` инициализируется до построения `ModuleItemViewModel`, в VM добавлены null-guards.

### v2.3 11.02.2026
- Удалён `ProfilesDirectory` из настроек приложения и UI; служебный путь профилей фиксирован как `DataDirectory/profiles`.

### v2.2 11.02.2026
- Введён `ModuleCatalog` с `ModuleSuffix`, имя конфигурации всегда `UserName_ModuleSuffix`.
- Стабилизирован compiled binding в HTTP модулях через `x:Static ...MethodOptions`.

### v2.1 11.02.2026
- CI lint в `.github/workflows/ci.yml` сделан точечным (проверяет только `Mode` внутри FluentTheme).

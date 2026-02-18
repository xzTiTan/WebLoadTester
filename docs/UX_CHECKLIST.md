# UX Regression Checklist (Stage 9)

Источник требований: `docs/Каркас.md` (v2.1 18.02.2026).

Формат:
- **OK** — требование явно реализовано в коде.
- **RISK** — выглядит реализованным, но есть хрупкость/пограничный сценарий.
- **BUG** — явное нарушение требования.

## 1) Layout / Shell / Workspace

1. [OK] Main shell: Header + Tabs + TabContent + LogDrawer присутствуют.
2. [OK] Workspace для UI/HTTP/NetSec — 3 колонки: LeftNav | Workspace | Details.
3. [OK] Splitter’ы между колонками есть и резайзят колонки.
4. [OK] В центральной колонке Workspace используется один вертикальный `ScrollViewer`.
5. [OK] Внутри module workspace есть toggle `Details` и состояние видимости применяется.

## 2) Running locks

6. [OK] Tabs блокируются во время `Running`.
7. [OK] LeftNav (поиск + список модулей) блокируется во время `Running`.
8. [OK] Блоки редактирования (`TestCase`, `RunProfile`, `Module Settings`) блокируются во время `Running`.
9. [OK] `Stop` и действия с артефактами в `RunControl` остаются доступны.

## 3) RowListEditor

10. [OK] Hotkeys (`Ctrl+N`, `Delete`, `Ctrl+D`, `Alt+Up/Down`) scoped внутри list-контрола, не глобально.
11. [OK] Нет внутреннего вертикального скролла в RowListEditor (`VerticalScrollBarVisibility=Disabled`).
12. [OK] После изменения selection строка прокручивается в viewport (`ScrollIntoView`).
13. [OK] Правило удаления единственной строки (очистить вместо удаления) соблюдается в list VM (UiScenario и Stage 5 списки).
14. [RISK] Фокус в «первое поле» после Add/Duplicate не гарантируется универсально для всех row-templates (только стабильный selection + scroll).

## 4) UiScenario steps

15. [OK] Action-driven enable/disable для Selector/Value реализован.
16. [OK] Правило Delay: Selector/Value отключены, `DelayMs > 0` обязателен.
17. [OK] Inline row validation для шага выводится под строкой.

## 5) Validation / Start guard

18. [OK] Единый pipeline валидации: TestCase + RunProfile + SettingsVM (через `IValidatable`).
19. [OK] `Start` блокируется при наличии workspace validation errors.
20. [OK] Ошибки показываются в UI (в RunControl, без диалогов).

## 6) LogDrawer

21. [OK] `Esc` сворачивает LogDrawer (если раскрыт).
22. [OK] Состояние LogDrawer (expanded / only errors / filter text) сохраняется в layout-state.
23. [OK] Show-in-log (фильтр/раскрытие) реализован через `LogDrawerViewModel.ShowInLog`.

## 7) Repeat run

24. [OK] Repeat run не запускает тест автоматически.
25. [OK] Во время Running repeat action запрещён и отображается подсказка.
26. [OK] При repeat-run подставляются module/profile/settings и имя формируется как `*_repeat`.
27. [OK] При отсутствии/битом `report.json` repeat отключается, причина показывается текстом.
28. [OK] После repeat-run: фокус на Start + прокрутка workspace в начало.

## 8) Persisted layout state

29. [OK] Сохраняются `LeftNavWidth`, `DetailsWidth`, `IsDetailsVisible`, `IsLogExpanded`, `IsLogOnlyErrors`, `LogFilterText`.
30. [OK] Загрузка state безопасная: при битом `settings.json` используются дефолты.
31. [OK] Сохранение состояния выполняется с debounce (без записи на каждый символ/пиксель).

## 9) Empty states

32. [OK] Нет выбранного модуля → «Выберите модуль слева».
33. [OK] Нет запусков в Runs → «Нет запусков».
34. [OK] Нет артефактов у текущего run → «Артефактов нет».

---

## Итог Stage 9 самопроверки

- **BUG:** не обнаружены.
- **RISK:** 1 пункт (фокус в первое поле row-template после Add/Duplicate, без явного требования на Stage 9 исправлять как фичу).
- Исправления кода по Stage 9 **не вносились** (т.к. явных BUG не найдено).

## Быстрый smoke-чек вручную (для QA)

1. Запустить приложение, проверить shell и 3-колоночный workspace.
2. Изменить ширины панелей, фильтр логов, перезапустить приложение — убедиться, что состояние восстановилось.
3. Запустить любой тест: проверить locks (tabs/leftnav/editors disabled), при этом Stop/лог/артефакты доступны.
4. В Runs выбрать запуск и нажать Repeat run: убедиться, что автозапуск не произошёл, фокус на Start, workspace прокручен вверх.
5. При активном run открыть Runs: Repeat run disabled, есть подсказка «Остановите запуск, чтобы повторить.».

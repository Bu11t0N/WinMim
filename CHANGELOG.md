# Changelog

## 2026-10-30 — Working classic and partial XP profiles

- Added explicit apply/restore for NT 4.0, 98/SE, Me, 2000 and partial XP alongside Windows 95. Profiles now select real palettes, fonts, menus and pinned RetroBar taskbars; XP includes a Luna menu and blue taskbar with classic window frames.
- Added isolated runtime ownership, per-field RetroBar settings recovery and serialization of classic desktop operations. Atomic JSON writes now tolerate short reader locks.
- Windows 1.x–3.x and NT 3.x remain outside this implementation. Shared icons and incomplete historical surfaces are labeled in the interface and documentation.

## 2026-10-30 — Classic95 UI, original icons and journal memory

Moved interactive classic apply/restore off the UI thread. Added process-local classic rendering and disabled modern taskbar hover previews for the Classic95 preset. Added a verified importer for private original Windows 95 icons; Microsoft resource binaries are not distributed. Recovery lists now avoid loading binary backups, with about 83% lower managed allocations in the documented synthetic comparison. Real UI cycle tests complement the earlier console/reboot checks. See docs/CLASSIC95-RELIABILITY-20261030.md.

## 2026-10-29 — Classic95 reliability fixes

Fixed blocking dispatcher waits during startup, reuse of the asynchronous session helper, palette validation ordering, guest integration process exclusions and rollback of DLLs still mapped after engine shutdown. Added regression tests including a real mapped-image rollback and integrity-checked deferred cleanup. See docs/CLASSIC95-BUGFIXES-20261029.md.

## 2026-10-29 — native UI, Classic95 and repository preparation

Added system-native Windows Forms controls, the future appearance catalog, a journaled Windows 7 sound adapter and an experimental Windows 95 profile. Integrated the maintainer-supplied multi-resolution icon. Added English project/build/contribution documentation and upstream research.

Local validation: 51 Core tests, 8 Python tests, 53 native DLLs and a resource-independent package stage/rollback smoke check. Recovered the main VM to Seven and verified a normal reboot; Windows 95 cold boot remains unresolved. See docs/VM-RECOVERY-20261029.md.

Earlier implementation notes below are retained in their original language.

## 24 октября 2026 — классическое поведение Seven

Семь дополнительных закреплённых upstream-модулей; собственные переходы оболочки и резервное окно свойств панели; согласование нативных свойств с Open-Shell; сохранение пользовательских настроек панели. Добавлены ограниченный системный адаптер с возвратом и восстановление внешнего Display provider при запуске. Уменьшены память истории и дублирование резервных копий. Проверены 20 модулей основного профиля, отдельный вызов классических свойств, параметры панели и системный откат. Подробности: docs/SEVEN-BEHAVIOR-20261024.md.

## 23 октября 2026 — живой baseline и диагностика процессов

Сохранены исходный замер клона (6592 файла, 34 области реестра), его сводка и снимок перед r8p. Исправлены зависание сбора файлов через cmdlet, точность покрытия при отказе доступа и ложные удаления в сравнении неполных областей. Добавлен прогресс. Путь к процессу в ShellHost и перезапуске Explorer теперь читается через QueryFullProcessImageName без перечисления его DLL. 26 проверок Core и четыре проверки сравнения пройдены. Full build 1137 проверен: таймаут Microsoft Symbols 0x80072EE2 и собственный откат. Добавлены снимок ошибки, readback активной темы через desktop-inspect и три попытки временных сетевых ошибок в нашем загрузчике PDB (три дополнительных теста). Успешная установка оригинала остаётся открытой.

## 22 октября 2026 — сравнение с оригинальным установщиком

Добавлены read-only инвентаризация установщика r8p, сборщик заданных областей реестра/файлов/SDDL/служб/задач и сравнение состояний с учётом ошибок доступа. Зафиксированы build 1137, SHA-256, 54 файла исходников модулей и 10 файлов реестра. Создан отдельный linked clone чистой ВМ. Четыре теста сравнения пройдены; динамическая установка оригинала пока не завершена.

## 22 октября 2026 — нативное применение и эталон Windows 7

Оформление переведено из отдельного PowerShell-журнала в Core: файлы, типы HKCU, visual style и обои применяются и возвращаются одной транзакцией. Повторное применение идемпотентно, поздние изменения сохраняются как конфликты. Добавлены CLI desktop-команды и перезапуск Проводника из интерфейса. По пользовательским снимкам Windows 7 скорректированы размеры и стекло «Пуска», добавлены воспроизводимые ярлыки системных программ. 25 автоматических проверок; отдельный живой тест проверяет нативное применение, чтение ярлыков Windows и точный возврат.

## 22 октября 2026 — лабораторное оформление Seven

В ВМ установлен и показан составной профиль с Aero10 Seven, Open-Shell, SecureUxTheme и OpenGlass. Добавлены проверяемая загрузка внешних пакетов, ограниченный тестовой ВМ установщик, сценарий настройки/возврата HKCU и действия в разделе «Компоненты». Подготовлен отдельный кеш DWM: OpenGlass требует плоскую структуру, отличающуюся от Windhawk. Тема остаётся внешней зависимостью со своей лицензией; готовность полного аналога не заявляется. Состояние и снимок экрана: `docs/SEVEN-DESKTOP-LAB.md`.

## 0.1.0 — 21 октября 2026

Первая разработческая сборка: интерфейс WPF, профили и настройки, управление изолированным portable host, журнал и откат, CLI, 23 модуля upstream, собственные инструменты и инфраструктура воспроизводимой сборки. На Windows 10 19044.1288 в VMware подтверждены загрузка модулей Seven/Vista и откат; добавлены VM-исполнитель и подготовка символов, исправлены размер окна на небольшом экране и тестовая фикстура junction без повышения прав. До полного соответствия Revert8Plus остаются визуальные ресурсы, системные функции, дополнительные программы и расширенные испытания; подробности в `docs/FEATURES.md` и `docs/VERIFICATION.md`.


## 23 октября 2026 — короткая проверка рабочего Seven

Добавлена изоляция перечисления DLL в VmSmoke и выбор отдельных модулей. В основной ВМ отдельно Aerexplorer и весь Seven прошли запуск/загрузку DLL/остановку/откат с нулём ошибок. Классическая панель Проводника подтверждена снимком. Перезапуск Explorer при работающем наборе остаётся отдельной проверкой.


## WinMim — первый мастер по согласованному ТЗ

Основной интерфейс заменён мастером Windows 7. Добавлены SevenSetup и команды seven-check/seven-apply: проверка компонентов, остановка host, подготовка Seven, применение оформления, обновление Explorer и запуск модулей. При ошибке возвращаются изменения текущего прохода; прежний журнал оформления при повторном применении сохраняется. Учтено имя SecureUxTheme4.dll. Дочерний host получил отдельные каналы вывода, чтобы не удерживать завершение CLI. Сборка: 27 тестов Core. Установка зависимостей на чистую систему ещё не автоматизирована.


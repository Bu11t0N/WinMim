# Проверка в VMware

Для живых испытаний используется отдельная ВМ `AEROFORGE-LAB`. Конфигурация: Windows 10 IoT Enterprise LTSC 2021 x64, 2 виртуальных процессора, 3 ГБ ОЗУ, растущий диск 60 ГБ, UEFI. Сетевой адаптер, общие папки, общий буфер обмена и drag-and-drop отключены. Исходный ISO пользователя не изменяется. Перед испытаниями требуется снимок чистой установленной Windows с VMware Tools и .NET Desktop Runtime.

`tests/AeroForge.VmSmoke` — отдельный интеграционный исполнитель. Он отказывается работать вне компьютера с именем `AEROFORGE-LAB`, без `--i-am-in-test-vm` и с повышенными правами. Не запускайте его на рабочей Windows и не переименовывайте рабочий компьютер для обхода проверки.

Сборка:

```powershell
dotnet publish tests/AeroForge.VmSmoke -c Release -o build/vm-smoke
```

Запуск из обычной интерактивной сессии тестового пользователя внутри ВМ:

```powershell
C:\AeroForgeLab\vm-smoke\AeroForge.VmSmoke.exe --i-am-in-test-vm --content C:\AeroForgeLab\AeroForge\content --output C:\AeroForgeLab\vm-smoke.json
```

Исполнитель проверяет SHA-256, готовит Seven и Vista, запускает тот же `ShellHost`, что использует интерфейс, открывает Проводник, записывает реально загруженные DLL, останавливает движок и откатывает транзакции. Отсутствующие ожидаемые модули и оставшиеся управляемые файлы считаются ошибками. Артефакты, созданные самим движком, регистрируются отдельно и не удаляются безусловно.

## Символы в ВМ без сети

При недоступности символов движок создаёт записи `pdb_…=error:…` в своих `ModsWritable/*.ini`. Внутри лабораторной ВМ запустите `tools/collect_vm_symbols.ps1` и перенесите `C:\AeroForgeLab\native-metadata.json` на машину с сетью. Скрипт читает только каталоги этого лабораторного исполнителя.

```powershell
python tools/fetch_vm_symbols.py --metadata native-metadata.json --store work/symbols --archive work/symbols.zip
```

Загрузчик использует только HTTPS-сервер Microsoft, проверяет MSF PDB, точное совпадение GUID и возраста DBI, допустимый возраст PDB Info, записывает SHA-256 в manifest.json. Правила соответствуют [Microsoft PDB1::OpenValidate4](https://github.com/microsoft/microsoft-pdb/blob/master/PDB/dbi/pdb.cpp). Источник символов описан в [документации Microsoft](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/microsoft-public-symbols).

Распакуйте symbols.zip в `C:\AeroForgeLab` внутри ВМ. Исполнитель копирует `C:\AeroForgeLab\Symbols` в кеш каждого тестового host перед запуском; путь можно переопределить через `--symbols DIR`. Если инициализация выявит дополнительные PDB, повторите сбор метаданных и догрузку. В этой сессии понадобились 14 PDB. Эти файлы остаются локальными зависимостями испытаний и не входят в исходный или portable-архив AeroForge.

Эта проверка подтверждает только перечисленные наблюдения. Загрузка DLL не доказывает правильность всех перехватов и визуального поведения. Basic/Classic и модули системных процессов, сторонние компоненты, обновление Windows, несколько мониторов и реальные сочетания клавиш требуют отдельных сценариев.

# Журнал выполнения ревизии PythonClient

**Ветка:** `dev`
**Основание:** `plan.md` (ревизия ADO.NET x64 provider поверх pythonnet 3.0.5)
**Целевая платформа:** .NET Framework 4.8, x64, pythonnet 3.0.5

Легенда статусов: ✅ выполнено · ⏳ в работе · ⛔ заблокировано · ⬜ не начато

## Окружение (проверено)

| Компонент | Значение |
|---|---|
| MSBuild | `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe` (18.10.1.42706) |
| VSTest | `C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe` (18.10.0) |
| dotnet SDK | 10.0.401 |
| nuget.exe | `%TEMP%\nugetcli\nuget.exe` (скачан с dist.nuget.org, в репозиторий не добавляется) |
| Python / pandas | **не установлены** (в PATH только Store-заглушка) |

## Ограничения и особенности

1. **Интеграционные тесты (`TestCategory=Python`) в этом окружении не выполняются** — нет Python.
   Для их прогона нужен `PYTHONNET_TEST_DLL=<путь к python3xx.dll>` и установленные `numpy`/`pandas`.
   Такие тесты пишутся, но помечаются как «не прогонялись».
2. **`msbuild /t:restore` не восстанавливает `packages.config`** (проекты legacy-формата, в MSBuild отсутствует
   поддержка packages.config-restore; при явном `/p:RestorePackagesConfig=true` — ошибка «Решение не найдено»).
   Используется `nuget.exe restore <packages.config> -PackagesDirectory packages`.
3. Папка `packages/` покрыта `.gitignore`, в репозиторий не попадает.

## Команды проверки (используются на каждом шаге)

```powershell
$msbuild = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$vstest  = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe'

# восстановление пакетов (после изменения packages.config)
& "$env:TEMP\nugetcli\nuget.exe" restore 'Pike.PythonClient64\packages.config' -PackagesDirectory 'packages' -NonInteractive
& "$env:TEMP\nugetcli\nuget.exe" restore 'Pike.PythonClient.Test\packages.config' -PackagesDirectory 'packages' -NonInteractive

# сборка
& $msbuild 'PythonClient.sln' /p:Configuration=Debug '/p:Platform=Any CPU' /m /v:m /nologo

# быстрый набор тестов (не требует Python)
& $vstest 'Pike.PythonClient.Test\bin\Debug\Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Unit'

# интеграционные тесты (требуют установленный Python)
# $env:PYTHONNET_TEST_DLL = 'C:\Python311\python311.dll'
# & $vstest 'Pike.PythonClient.Test\bin\Debug\Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Python'
```

## План этапов

| Этап | Содержание | Статус |
|---|---|---|
| 0 | Подготовка: restore, baseline-сборка, журнал | ✅ |
| 1 | P0-1…P0-4: connection string, коллекции параметров, `PythonParameter` + Unit-тесты | ⬜ |
| 2 | P0-8, P0-9: конвертеры, реестр dtype, конвертация DataFrame | ⬜ |
| 3 | P0-6, P0-7: GIL на операцию, refcount движка, жизненный цикл соединения | ⬜ |
| 4 | P0-5: безопасная регистрация в `machine.config` | ⬜ |
| 5 | P1-1…P1-5: контракты `DbCommand`/`DbParameter`/`DbConnection`, контекст выполнения | ⬜ |
| 6 | P2-1…P2-6: SDK-style, вынос install, тестовая инфраструктура, документация, CI | ⬜ |

---

Формат записи (записи добавляются в конец файла):

```
### N.M <шаг> — ✅ (YYYY-MM-DD)

**Изменённые файлы:** ...
**Сделано:** ...
**Проверка:** ...
```

---

## Этап 0. Подготовка

### 0.1 Восстановление пакетов и baseline-сборка — ✅ (2026-10-29)

**Сделано**

- Скачан `nuget.exe` в `%TEMP%\nugetcli` (в репозиторий не добавляется).
- Восстановлены пакеты в корневую папку `packages/` (пути `HintPath` в csproj указывают на `..\packages\...`):
  - `Pike.PythonClient64`: `pythonnet 3.0.5`, `Microsoft.CSharp 4.7.0`, `System.Reflection.Emit 4.7.0`;
  - `Pike.PythonClient.Test`: `MSTest.TestAdapter 2.2.10`, `MSTest.TestFramework 2.2.10`.
- Собрано решение в конфигурации `Debug|Any CPU`: **успешно**.
  - `Pike.PythonClient64 -> Pike.PythonClient64\bin\Debug\Pike.PythonClient64.dll`
  - `Pike.PythonClient.Test -> Pike.PythonClient.Test\bin\Debug\Pike.PythonClient.Test.dll`
  - Baseline-предупреждение (1 шт.): `MSB3270` — тестовый проект `AnyCPU` ссылается на x64-сборку провайдера.
    Планируется устранить при переводе тестового проекта на `PlatformTarget=x64` (этап 6).

**Baseline-прогон тестов**

- `vstest.console.exe ... '/TestCaseFilter:TestCategory=Unit'` → «Нет тестов, соответствующих указанному
  фильтру»: категорий в существующих тестах нет. Это ожидаемо; Unit-набор появится на этапе 1.
- Существующие тесты (`Basic`, `Connection`, `DataProvider`, `DataQuery`, `DbProviderFactory`) требуют
  установленного Python (`SettingsMain.Default.PythonDllPath` = `C:\Users\Pike\anaconda3\python311.dll`)
  и в текущем окружении не запускаются. Исправление путей/категорий — этап 6 (P1-6).

### 0.2 Журнал `steps.md` — ✅ (2026-10-29)

Создан этот файл; ведётся после каждого шага (дата, файлы, команды, результат).

---

## Этап 1. P0-1…P0-4 — connection string, коллекции и параметры

### 1.1 P0-1: `PythonConnectionStringBuilder` — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\Data\PythonConnectionStringBuilder.cs`

**Сделано**
- Удалён собственный парсер (`ToUpperInvariant()` + `Substring`/`Replace`), который приводил значения к верхнему
  регистру и портил индексы для символов, меняющих длину при апперкейсе (`ß` → `SS`, `ı` → `I`).
- `Parse(string)` теперь собирает builder через базовый `DbConnectionStringBuilder.ConnectionString`: регистр
  значений сохраняется, работает квотирование (`PYTHONPATH="C:\Py\Lib;C:\Py\Lib\site-packages"`), формат
  валидируется фреймворком. `null` → `ArgumentNullException`, пустая строка → «нет настроек» (пустой builder).
- `ResolveKey(string)` — поиск ключа `OrdinalIgnoreCase`: фреймворк передаёт ключи в нижнем регистре, поэтому
  `builder["file"]` и `builder["FILE"]` возвращают одно и то же; неизвестный ключ → `KeyNotFoundException`.
- Добавлен `Validate()` — проверка обязательного `PYTHONDLL` (`InvalidOperationException`); подключается
  в `PythonConnection.Open()` на шаге 5.3.

**Проверка**
- Сборка `Debug` — успешно, новых предупреждений нет.
- Smoke-скрипт на собранной сборке (вне репозитория): разбор
  `PYTHONDLL=C:\Py\python311.dll;PYTHONPATH="C:\Py\Lib;C:\Py\Lib\site-packages";FILE=C:\Scripts\TestScript.py`
  → значения совпадают байт-в-байт; round-trip через `ConnectionString` стабилен; `builder['file'] == builder['FILE']`;
  неизвестный ключ → `KeyNotFoundException`; `Parse("")` → 0 ключей; `Validate()` без `PYTHONDLL` →
  `InvalidOperationException`.

**Breaking change:** строки, где `PYTHONPATH` содержит `;` без кавычек, теперь отклоняются парсером (по
спецификации ADO.NET). Требуется запись в README/CHANGELOG (этап 6).


### 1.2 P0-4 (часть): `KeyIndexCollection` — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\KeyIndexCollection.cs`

**Сделано**
- Добавлены `Set(key, value)` (замена значения без смены позиции либо добавление в конец), `Insert(index, key, value)`
  (вставка по индексу; существующий ключ перемещается на указанную позицию), `TryGetValue(key, out value)`, `Keys`.
- `Values` и `GetEnumerator()` теперь отдают элементы **в порядке добавления** (ранее — порядок `Dictionary`,
  который не гарантирован после удалений).
- `Remove` для отсутствующего ключа — no-op (задокументировано), `RemoveAt` по-прежнему валидирует индекс.

**Проверка**
- Сборка `Debug` — успешно.
- Smoke-скрипт: `Add a,b` → `Set c` → `Set a` (значение заменено, позиция сохранена) → `Insert(0,z)` → `Insert(1,b)`
  даёт `Keys = z,b,a,c`, `Values = 0,22,11,3`, `IndexOfKey('b') = 1`, `TryGetValue('missing') = false`,
  `Remove('missing')` не меняет `Count`, перечисление совпадает с `Keys`/`Values`.


### 1.3 P0-2 + P0-4: `PythonParameterCollection` — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\Data\PythonParameterCollection.cs`

**Сделано**
- `CopyTo(Array, int)` переписан: проверки `null`/`index < 0`/нехватки места, копирование всех элементов
  в порядке коллекции с `index + i` (ранее при `index > 0` копировались не те элементы и менялся `index` в теле цикла).
- `SetParameter(string, DbParameter)` использует `KeyIndexCollection.Set` — `Parameters[name] = value` теперь
  **заменяет** существующий параметр, а не падает с `ArgumentException("The given key is already exist")`.
- `Insert(int, object)` вставляет параметр по индексу (через `KeyIndexCollection.Insert`) с валидацией индекса,
  типа и `ParameterName` (ранее `index` игнорировался, вызывался `Add`).
- `Contains(object)`/`IndexOf(object)`/`Add(object)`/`SetParameter` используют `as PythonParameter` и дают
  `ArgumentException` с понятным сообщением вместо `InvalidCastException`; `Contains(object)`/`IndexOf(object)`
  для `null`/чужого типа возвращают `false`/`-1`.
- `Remove(object)` для отсутствующего параметра — no-op (ранее падал); `Add` по-прежнему требует уникальный ключ.
- `IsSynchronized => false` (блокировок в коллекции нет); `GetEnumerator()` возвращает параметры (а не пары
  «ключ-значение»), что соответствует контракту `DbParameterCollection` и ожиданиям `DbDataAdapter`.

**Проверка**
- Сборка `Debug` — успешно, новых предупреждений нет.
- Smoke-скрипт: `Parameters['b'] = newParam` → `Count` не изменился, значение заменено; `Insert(1, …)` даёт порядок
  `a,z,b,c`; `CopyTo(array, 1)` даёт `keep,a,z,b,c` (элемент до `index` не изменён); маленький целевой массив →
  `ArgumentException`; `Contains("not a parameter") = false`, `IndexOf(...) = -1`, `IndexOf(null) = -1`;
  `Remove(missing)` — no-op; перечисление коллекции даёт только `PythonParameter`.


### 1.4 P0-3 + P1-2: `PythonParameter` и переиндексация при переименовании — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\Data\PythonParameter.cs`,
`Pike.PythonClient64\Data\PythonParameterCollection.cs`, `Pike.PythonClient64\KeyIndexCollection.cs`

**Сделано**
- P0-3: `Value = null` и `Value = DBNull.Value` больше не падают (`NullReferenceException` в `_value.GetType()` и
  `(string)value` в `GetSize`); для них `DbType = String`, `Size = 0`. `GetSize` использует `as string`/`as byte[]`,
  неизвестный тип возвращает `0`, а не `SystemException`.
- P1-2: `DbType` теперь settable и используется как подсказка (явно заданный `DbType` не перетирается присвоением
  `Value`; `ResetDbType()` возвращает автоматическое определение).
- P1-2: `Direction` валидируется — `Output`/`InputOutput`/`ReturnValue` → `NotSupportedException`.
- P1-2: `IsNullable` по умолчанию `true`; `ParameterName` стал обычным settable (без исключений), плюс
  `PythonParameterCollection.EnsureIndex()`/`KeyIndexCollection.RenameKey()` переиндексируют параметр при
  переименовании (позиция сохраняется, коллизия имён не роняет и не «схлопывает» коллекцию).
- P1-2: `ParseDbType` расширен (`Byte`, `SByte`, `Int16`, `UInt16/32/64`, `Single`, `Decimal`, `Char`, `Guid`,
  `TimeSpan` → `DbType.Time`, `DateTimeOffset`, `byte[]` → `DbType.Binary`); неизвестный тип →
  `NotSupportedException` с указанием типа.

**Проверка**
- Сборка `Debug` — успешно, новых предупреждений нет.
- Smoke-скрипты:
  - `Value = null` → `DbType=String, Size=0`; `DBNull` → `String/0`; строка `"Pike"` → `Size=4`;
  - 14 типов дают ожидаемые `DbType`; `Value = new Uri(...)` → `NotSupportedException`;
  - `IsNullable = true` по умолчанию, `Direction = Output` → `NotSupportedException`, `ParameterName`
    переименовывается дважды;
  - переименование параметра после `Add` — `Contains('new') = true`, `IndexOf('new') = 0`, старое имя исчезло,
    коллизия имён не меняет `Count`, `Parameters['new'] = p` не увеличивает коллекцию.


### 1.5 Unit-тесты для P0-1…P0-4 — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient.Test\Tests\Unit\ConnectionStringBuilderUnit.cs`,
`Tests\Unit\KeyIndexCollectionUnit.cs`, `Tests\Unit\ParameterCollectionUnit.cs`, `Tests\Unit\ParameterUnit.cs`,
`Pike.PythonClient.Test\Pike.PythonClient.Test.csproj`

**Сделано**
- Добавлен Unit-набор (48 тестов, `[TestCategory("Unit")]`, Python не требуется), имена по схеме `P0_x_...`/`P1_x_...`:
  - **Connection string (P0-1)**: сохранение регистра значений, round-trip, case-insensitive ключи, разбор
    `PYTHONPATH`, неизвестный ключ → `KeyNotFoundException`, строка без `=` и `;` без кавычек → `ArgumentException`,
    `null` → `ArgumentNullException`, пустая строка → пустой builder, юникод в пути, `;` в конце, пустое значение,
    состав `Keys`, `Validate()`.
  - **KeyIndexCollection (P0-4/P1-2)**: `Set` (замена/добавление), `Insert` (новый ключ/перемещение), валидация
    индекса, порядок `Values`/перечисления, `TryGetValue`, `Remove` отсутствующего ключа, дубликаты, `RenameKey`
    (позиция/дубликат/отсутствующий ключ).
  - **PythonParameter (P0-3/P1-2)**: `null`/`DBNull`, `Size` для строки, вывод `DbType` для 13 типов, `byte[]`,
    неподдерживаемый тип → `NotSupportedException`, явный `DbType` и `ResetDbType`, `Direction`, `IsNullable`,
    переименование.
  - **PythonParameterCollection (P0-2/P0-4/P1-2)**: `Parameters[name] = value`, дубликаты в `Add`, `Insert` и его
    границы, `CopyTo` (смещение/маленький массив/`null`), безопасные `Contains(object)`/`IndexOf(object)`,
    `Remove` отсутствующего, перечислитель отдаёт параметры, `IsSynchronized = false`, переименование.
- Новые файлы внесены в `Pike.PythonClient.Test.csproj` (legacy-формат, явный список `<Compile>`).

**Проверка**
```
& $msbuild 'PythonClient.sln' /p:Configuration=Debug '/p:Platform=Any CPU' /m   # успешно, новых предупреждений нет
& $vstest 'Pike.PythonClient.Test\bin\Debug\Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Unit'
# Всего тестов: 48, Пройдено: 48, Не пройдено: 0
```

**Наблюдение по поведению базового парсера:** `"PYTHONPATH="` (пустое значение) не создаёт ключ —
`DbConnectionStringBuilder` трактует его как отсутствующий. Тест `P0_1_ConnectionString_EmptyValueIsTreatedAsMissing`
фиксирует это как ожидаемое поведение («пустое значение = не задано»).


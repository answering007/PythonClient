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
| 1 | P0-1…P0-4: connection string, коллекции параметров, `PythonParameter` + Unit-тесты | ✅ |
| 2 | P0-8, P0-9: конвертеры, реестр dtype, конвертация DataFrame | ✅ |
| 3 | P0-6, P0-7: GIL на операцию, refcount движка, жизненный цикл соединения | ✅ |
| 4 | P0-5: безопасная регистрация в `machine.config` | ✅ |
| 5 | P1-1…P1-5: контракты `DbCommand`/`DbParameter`/`DbConnection`, контекст выполнения | ✅ |
| 6 | P2-1…P2-6: SDK-style, вынос install, тестовая инфраструктура, документация, CI | ✅ |

## Итоги (2026-10-29)

Все этапы плана (P0, P1, P2) выполнены в ветке `dev`.

| Проверка | Результат |
|---|---|
| `dotnet build PythonClient.sln -c Debug` / `-c Release` | успешно, 0 предупреждений, 0 ошибок |
| `msbuild PythonClient.sln /restore` (legacy-путь) | успешно |
| `vstest --TestCaseFilter:TestCategory=Unit` | **85 / 85 пройдено** |
| `vstest --TestCaseFilter:TestCategory=Python` | **46 / 46 пройдено** (Python 3.11.16, numpy 2.4.6, pandas 3.1.0, локальное окружение через `uv`) |
| `scripts\smoke\Test-ProviderSmoke.ps1` | **40 / 40 проверок** |

Остаточные риски и что ещё проверить:

1. ~~Прогон набора `TestCategory=Python`~~ — выполнено, см. **Этап 7** (46/46 тестов, Python 3.11.16 / pandas 3.1.0).
   Матрицу версий стоит расширить: pandas 1.5/2.x и Python 3.9/3.13 (`-PythonVersion` в
   `scripts\test\Initialize-PythonEnvironment.ps1`).
2. Проверить сценарии Power Query на примерах `Pike.PythonClient.Test\Excel\PowerQuery - 01..04.xlsx`
   после `scripts\install.ps1` (требуется 64-битный Excel и права администратора).
3. `PyObject`-обёртки отдельных элементов `tolist()` освобождаются только GC (зафиксировано в 2.4) — при больших
   DataFrame стоит оценить потребление памяти и при необходимости перейти на типизированный доступ по numpy-буферам.
4. `key.snk` остаётся в репозитории (решение зафиксировано в `SECURITY.md`); при ротации ключа изменится
   `PublicKeyToken`, потребуется повторная регистрация провайдера.
5. `PythonClient64Setup.vdproj` не удалён, но считается устаревшим: поставка — `scripts\install.ps1`/`uninstall.ps1`.
6. `PythonEngine.Shutdown()` с импортированными numpy/pandas не всегда финализирует интерпретатор
   (`PythonEngine.IsInitialized` может остаться `true`); провайдер трактует финализацию как best-effort и
   полагается на счётчик ссылок. Для Excel/PQ рекомендуется `PythonDataProvider.ShutdownOnLastClose = false`.


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


---

## Этап 2. P0-8, P0-9 — конвертеры и конвертация DataFrame

### 2.1 P0-8: `DateTimeConverter`/`TimeSpanConverter` + `PythonValue` — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\ColumnConverters\PythonValue.cs` (новый),
`ColumnConverters\DateTimeConverter.cs`, `ColumnConverters\TimeSpanConverter.cs`, `Pike.PythonClient64.csproj`

**Сделано**
- Новый внутренний helper `PythonValue` — единая точка разбора «сырых» значений: `IsNone()` (null, `DBNull`,
  python `None`), `TryToInt64()` (терпимо к `long/int/short/byte/sbyte/ushort/uint/ulong`, `DateTime`, `TimeSpan`,
  `PyInt`, `PyObject`), `TryToDouble()`, `TryToBoolean()`, `ToManagedString()`. Исключения конвертации
  перехватываются и трактуются как «значение не сконвертировано».
- `DateTimeConverter`: numpy `datetime64` → `DateTime` через деление наносекунд на 100 (тики) **без** создания
  промежуточного `new DateTime(ticks)`, поэтому даты до 1970 больше не падают; `NaT` (`long.MinValue`) и значения
  вне диапазона `DateTime` → `DBNull`; значение, уже пришедшее как `DateTime`, возвращается как есть.
- `TimeSpanConverter`: аналогично, с проверкой диапазона `TimeSpan` (переполнение → `DBNull`), поддержкой
  готового `TimeSpan` и `NaT`.

**Проверка**
- Сборка `Debug` — успешно, новых предупреждений нет.
- Smoke-скрипт на собранной сборке (без Python):
  - `datetime[0] = 1970-01-01`, `1.6e18 ns → 2020-09-13 12:26:40`, `-315619200000000000 ns → 1960-01-01`
    (ранее `ArgumentOutOfRangeException`), `long.MinValue (NaT)`, `null`, `DBNull` → `DBNull`, готовый
    `DateTime` проходит без изменений;
  - `timedelta`: `3.6e12 ns → 01:00:00`, `-3.6e12 ns → -01:00:00`, `NaT`/`null` → `DBNull`.


### 2.2 P0-9 (P2-6): реестр dtype `SupportedTypes` — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\SupportedTypes.cs`

**Сделано**
- `SupportedTypes` стал статическим реестром с ленивой потокобезопасной инициализацией (`Lazy<T>` + `lock`),
  `Values` теперь `IReadOnlyDictionary<string, IColumnConverter>` (внешняя мутация глобального состояния невозможна).
- `NormalizeDtype`: снимает numpy byte-order маркер (`<i8`, `>i8`, `|O`) и параметры dtype
  (`datetime64[ns, UTC]` → `datetime64`, `string[python]` → `string`, `timedelta64[ns]` → `timedelta64`).
- Реестр использует `StringComparer.OrdinalIgnoreCase`, поэтому nullable extension dtypes (`Int8/Int32/Int64`,
  `UInt*`, `Float*`, `boolean`) попадают на те же конвертеры, что и numpy-аналоги.
- Покрытие dtype: `bool/boolean`, `int8/int16/int32/uint8/uint16/uint32` → `IntegerConverter`,
  `int64/uint64` → `LongConverter`, `float32/float64` → `DoubleConverter`, `datetime64`/`timestamp` →
  `DateTimeConverter`, `timedelta64` → `TimeSpanConverter`, `object/str/string/category/unicode` → `StringConverter`.
- `TryGetConverter`/`GetConverter` (неизвестный dtype → `NotSupportedException` — вместо молчаливого
  превращения в строку), `Register(converter, params string[] dtypeNames)`, `Reset()`.

### 2.3 P0-9: конвертеры колонок на основе `PythonValue` — ✅ (2026-10-29)

**Изменённые файлы:** `ColumnConverters\IntegerConverter.cs`, `LongConverter.cs`, `DoubleConverter.cs`,
`BooleanConverter.cs`, `StringConverter.cs`

**Сделано**
- Все конвертеры переведены на `PythonValue` (никаких прямых приведений `(PyInt)obj`, которые падали на
  `None`/`pd.NA`/`np.nan`).
- Единая политика: `None`/`null`/`DBNull` → `DBNull`; `np.nan` → `DBNull`; значение вне диапазона целевого типа
  (`int`) → `DBNull`; непонятное значение → `DBNull` вместо исключения.
- `StringConverter` больше не вызывает `ToString()` у `None` (раньше это давало строку `"None"`).

**Проверка**
- Сборка `Debug` — успешно, новых предупреждений нет.


### 2.4 P0-9: конвертация `DataFrame` → `DataTable` — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\PythonDataQuery.cs`

**Сделано**
- `RunScript`: результат скрипта освобождается (`using (PyObject result = AsPyObject(variables[ResultKey]))`) —
  ранее объект `result` утекал.
- `ConvertDataFrameToDataTable(PyObject)` вместо `dynamic`: проверка результата (должен иметь `columns`/`dtypes`,
  иначе `InvalidOperationException("'result' must be a pandas.DataFrame, got <type>")`), работа через
  типизированный `PyObject` API с `using` для `df.columns`, списка имён, `df[column]`, `dtype`, `.values`, `.tolist()`.
- dtype берётся у конкретной колонки (`series.dtype`) и нормализуется через `SupportedTypes`; неизвестный dtype →
  `NotSupportedException("Column '<name>' has unsupported dtype '<dtype>'")` (управляется `PythonDataQuery.StrictColumnTypes`,
  по умолчанию `true`; `false` — fallback на строковый конвертер).
- Уникальные и корректные имена колонок: пустые → `Column{i}`, символы `[`/`]`/`.` заменяются, дубликаты получают
  суффикс `_2`, `_3` (ранее дубликаты давали `DuplicateNameException`, а имена с `[`/`]`/`.` — исключение).
- Колонки `datetime64`/`timedelta64` читаются как точные наносекунды (`series.astype("int64")`), поэтому
  значения не теряют точность и корректно дают `DBNull` на `NaT`; для tz-aware (`datetime64[ns, UTC]`) приведение
  к `int64` невозможно — используется fallback на питоновские значения.
- Заполнение таблицы: `BeginLoadData()`/`EndLoadData()` + `Rows.Add(object[])`, количество строк ограничено
  минимальной длиной колонок (защита от рассинхронизации).
- `StrictColumnTypes` — новый публичный флаг (по умолчанию `true`).

**Ограничение (зафиксировано):** PyObject-обёртки отдельных элементов `object[]`, полученных из `tolist()`,
по-прежнему освобождаются только сборщиком мусора. Массовое освобождение элементов требует отдельного
прогона с реальным pandas (риск refcount-ошибок без тестов), поэтому вынесено за рамки этого шага.

**Проверка**
- Сборка `Debug` — успешно, новых предупреждений нет.
- Интеграционная проверка (нужен Python) — см. тесты категории `Python`; в текущем окружении не выполнялась.


### 2.5 Unit-тесты конвертеров и реестра dtype — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient.Test\Tests\Unit\ColumnConverterUnit.cs`,
`Tests\Unit\SupportedTypesUnit.cs`, `Pike.PythonClient.Test\Pike.PythonClient.Test.csproj`

**Сделано**
- `ColumnConverterUnit` (13 тестов): `datetime64`-наносекунды → `DateTime` (1970, 2020, 1960), `NaT`/`null`/`DBNull`
  → `DBNull`, готовый `DateTime` проходит как есть; `timedelta64` → `TimeSpan` (±1 час), `NaT`; целочисленные
  конвертеры с `null` и переполнением; `double` с `NaN`; `bool` с `null`/строкой; `string` с `null`;
  проверка `TargetType` всех конвертеров.
- `SupportedTypesUnit` (5 тестов): нормализация dtype, поддержка numpy и nullable-extension dtype,
  соответствие dtype → тип конвертера, `NotSupportedException` для `complex128`, `Register`/`Reset`,
  read-only природа `Values` и общий экземпляр конвертера для `bool`/`boolean`.
- `[TestCleanup]` возвращает реестр в исходное состояние, чтобы тесты не влияли друг на друга.

**Проверка**
```
& $msbuild 'PythonClient.sln' /p:Configuration=Debug '/p:Platform=Any CPU' /m   # успешно
& $vstest 'Pike.PythonClient.Test\bin\Debug\Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Unit'
# Всего тестов: 65, Пройдено: 65, Не пройдено: 0
```

**Этап 2 завершён.** Все дефекты P0-8/P0-9 исправлены; интеграционные проверки с реальным pandas
(категория `Python`) остаются непройденными в этом окружении из-за отсутствия Python.


---

## Этап 3. P0-6, P0-7 — GIL и жизненный цикл движка

### 3.1–3.3 P0-6/P0-7: GIL на операцию и refcount движка — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\PythonDataProvider.cs`, `Pike.PythonClient64\PythonDataQuery.cs`,
`Pike.PythonClient64\Data\PythonConnection.cs`

**Сделано**
- Удалён статический `GILState`, который захватывался один раз в `Open()` и удерживался до `Close()` —
  источник `AccessViolationException`/порчи интерпретатора при вызовах из другого потока.
  Теперь GIL захватывается на каждую операцию: `using (Py.GIL())` в `RunScript` и в `AddPathComponents`.
- `PythonDataProvider.Open` работает под `lock (Sync)` и со счётчиком ссылок: повторный `Open()` при живом
  движке увеличивает счётчик и добавляет новые пути (ранее молча игнорировал дополнительные пути).
- `Close()` уменьшает счётчик и вызывает `PythonEngine.Shutdown()` **только** когда счётчик дошёл до нуля и
  `ShutdownOnLastClose == true`; появился `ShutdownOnLastClose` (по умолчанию `true` для обратной
  совместимости; для Excel/PQ рекомендуется `false`) и принудительный `Shutdown()`.
- Инициализация отказоустойчива: `File.Exists(PythonDllPath)` до `Initialize()`, при исключении между
  `Initialize()` и настройкой путей выполняется `Shutdown()` и `State = Closed` (раньше движок оставался
  поднятым с `State = Closed`, и следующий `Open()` ничего не инициализировал).
- `DefaultPathComponents`: пути фильтруются по `Directory.Exists`, добавлена поддержка `PYTHONHOME`/`PYTHONPATH`,
  дубликаты убираются; пути добавляются через `sys.path.insert(0, …)` (приоритет пользовательских путей).
- `Version` кэшируется при инициализации и возвращает пустую строку на закрытом движке (раньше обращался к
  `Py_GetVersion()` вне инициализированного интерпретатора).
- `PythonConnection` запоминает собственное состояние (`_isOpen`): `State`, `ServerVersion` и `Close()`
  больше не зависят от глобального состояния; двойной `Close()` не освобождает чужую ссылку,
  `Dispose` закрывает соединение только при `disposing == true`.

**Проверка**
- Сборка `Debug` — успешно, новых предупреждений нет.
- `vstest '/TestCaseFilter:TestCategory=Unit'` → 65/65 пройдено (регрессий нет).
- Интеграционная проверка P0-6/P0-7 требует Python (см. 3.4).

### 3.4 Интеграционные тесты жизненного цикла + `TestEnvironment` — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient.Test\TestEnvironment.cs` (новый),
`Tests\Python\EngineLifecycleTests.cs` (новый), `Pike.PythonClient.Test\Pike.PythonClient.Test.csproj`

**Сделано**
- `TestEnvironment`: путь к `python*.dll` из `PYTHONNET_TEST_DLL` с фолбэком на настройки, `IsPythonAvailable`,
  `SkipIfNoPython()` (→ `Assert.Inconclusive` вместо падения), `CreateBuilder`/`CreateConnection`
  (используют публичный `PythonDataProvider.DefaultPathComponents`), `ShutdownIfOwned()`.
- `EngineLifecycleTests` (`[TestCategory("Python")]`): закрытие одного соединения не гасит движок для второго;
  движок гасится при закрытии последнего соединения; двойной `Close()` не освобождает чужую ссылку;
  `RunScript` из другого потока; параллельные соединения (`Parallel.For`) без исключений.
- Тестовый проект получил прямую ссылку на `Python.Runtime` (нужна для проверок `PythonEngine.IsInitialized`).

**Проверка**
```
& $vstest '...Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Unit'    # 65/65 пройдено
& $vstest '...Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Python'  # 5 тестов пропущено (Inconclusive)
```
Тесты корректно помечаются как пропущенные, т.к. `PYTHONNET_TEST_DLL` не задан и Python не установлен.
Прогон с реальным Python остаётся за CI/машиной с Python 3.11 + numpy/pandas.


---

## Этап 4. P0-5 — безопасная регистрация провайдера

### 4.1 P0-5 (+P2-2): `DataProviderInstallationBase` — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\Install\DataProviderInstallationBase.cs`,
`Pike.PythonClient64\Pike.PythonClient64.csproj`

**Сделано**
- `DbProviderFactories` создаётся внутри `system.data` (`GetOrCreate(data, xml, DbProviderFactories, data)`),
  а не в корне `configuration` — ранее это делало `machine.config` невалидным для .NET.
- Существующая запись провайдера **обновляется**: атрибуты `name`, `invariant`, `description`, `type`
  выставляются всегда (раньше обновлялся только при отсутствии узла).
- `type` стал версионно-независимым: `"{FullName}, {AssemblyName}"` вместо `AssemblyQualifiedName`
  (`Version=1.2.0.0, PublicKeyToken=…`), поэтому обновление сборки больше не ломает
  `DbProviderFactories.GetFactory`.
- Безопасная запись: `XmlDocument.Load` (сохраняется XML-декларация вместо `InnerXml = File.ReadAllText`),
  запись в `*.tmp` → повторная загрузка для валидации → бэкап `machine.config.bak-<utc>` →
  атомарная замена `File.Replace`; кодировка UTF-8 **без BOM** (`new UTF8Encoding(false)`).
- Пути к `machine.config` строятся из `RuntimeEnvironment.GetRuntimeDirectory()` (сегмент
  `Framework`/`Framework64` заменяется по требованию), зависимость от `Microsoft.Build.Utilities.v4.0`
  удалена — ссылка убрана из `Pike.PythonClient64.csproj` (часть P2-2).
- Публичные API получили XML-документацию с исключениями; `UnregisterProvider` идемпотентен.

**Проверка**
- Сборка `Debug` — успешно, предупреждений нет.
- Smoke-проверка путей: `Machine64Config = C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Config\machine.config`,
  `Machine32Config = C:\Windows\Microsoft.NET\Framework\v4.0.30319\Config\machine.config` (файлы существуют).

### 4.2 Unit-тесты регистрации на копии `machine.config` — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient.Test\Tests\Unit\DataProviderInstallationUnit.cs` (новый),
`Pike.PythonClient.Test\Pike.PythonClient.Test.csproj`

**Сделано**
- 9 тестов (без прав администратора, на временной копии конфигурации): создание узлов при отсутствии
  `system.data`; создание `DbProviderFactories` внутри `system.data`; обновление существующей записи со
  старой версией сборки (проверяется отсутствие `Version=` и точное значение `type`); идемпотентность;
  сохранение XML-декларации, бэкапа и отсутствие BOM; сохранение прочих секций (`startup`, `system.web`);
  удаление записи; no-op при отсутствии записи; корректность путей `Machine64Config`/`Machine32Config`.
- В тестовый проект добавлена ссылка `System.Xml`.

**Проверка**
```
& $msbuild 'PythonClient.sln' /p:Configuration=Debug '/p:Platform=Any CPU' /m   # успешно
& $vstest '...Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Unit'
# Всего тестов: 74, Пройдено: 74, Не пройдено: 0
```

**Найденный по ходу дефект:** первая версия `GetMachineConfigFile` формировала путь
`Framework64\Framework64\v4.0.30319` (сегмент добавлялся вместо замены) — исправлено и покрыто тестом.


---

## Этап 5. P1 — контракты ADO.NET

### 5.1–5.3 P1-1…P1-5: команда, контекст выполнения, параметры, соединение — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\PythonScriptContext.cs` (новый),
`Pike.PythonClient64\PythonDataQuery.cs`, `Pike.PythonClient64\Data\PythonCommand.cs`,
`Pike.PythonClient64\Data\PythonConnection.cs`, `Pike.PythonClient64.csproj`

**Сделано**
- **P1-5 (контекст)**: добавлен `PythonScriptContext` (`Query`, `Parameters`, `ResultName`, `ResultRequired`) и
  `PythonDataQuery.RunScript(scriptText, context)`; состояние выполнения больше не статическое.
  Старые `Query`/`Parameters`/`Reset()` сохранены как обёртки над `AsyncLocal`-контекстом и помечены `[Obsolete]`.
- **P1-5 (параметры)**: `null`/`DBNull` → `None` (раньше `ArgumentNullException`); срезаются префиксы `@`/`:`;
  обрабатываются `decimal` (→ `float`, в Python нет decimal), `Guid` (→ строка), `TimeSpan` (→ `timedelta64[ns]`),
  `DateTimeOffset` (→ UTC `datetime64[ns]`), `DateTime` с `Kind=Local` приводится к UTC; `PyDict` освобождается при
  ошибке конвертации.
- **P1-3 (команда)**: `DesignTimeVisible = false`; `DbConnection` можно сбросить в `null`; поддержаны
  `CommandBehavior.SchemaOnly` (схема без строк), `SingleRow`, `CloseConnection`; `CommandTimeout > 0` и
  `DbTransaction` → `NotSupportedException` (явное поведение вместо зависания); `ExecuteNonQuery` допускает скрипт
  без `result` (возвращает `0`), `ExecuteScalar` берёт первую строку результата; скрипт из файла читается как UTF-8
  (BOM определяется) и кэшируется по `(путь, LastWriteTimeUtc)`; `Cancel()` документирован как no-op.
- **P1-4 (соединение)**: `Open()` вызывает `Validate()` (`PYTHONDLL` обязателен) и запрещает работу с другим
  `python*.dll`, если движок уже инициализирован; `ConnectionString = null/""` → «нет настроек» (без исключения);
  `BeginDbTransaction`/`ChangeDatabase` → `NotSupportedException` вместо `NotImplementedException`; `ServerVersion`
  пуст на закрытом соединении.
- **P1-3/P1-5 (результат)**: `ConvertDataFrameToDataTable(dataFrame, resultName)` — имя таблицы из контекста;
  дублирующиеся метки колонок теперь дают понятный `NotSupportedException` (раньше `df[label]` возвращал DataFrame и
  падал внутри pyconverter), нормализация/уникализация имён сохранена.

**Проверка**
- Сборка `Debug` — успешно, предупреждений нет.
- Unit-набор дополнен `CommandUnit` (проверки контрактов без Python): `Connection = null`, `DesignTimeVisible`,
  исполнение без соединения/на закрытом соединении, `KeyNotFoundException` на неизвестный ключ строки,
  пустая строка соединения, `Open()` без `PYTHONDLL`, транзакции/`ChangeDatabase`, `ServerVersion`, идемпотентность
  `Close/Dispose` — **85/85 Unit-тестов пройдено**.


### 5.4 P1-6: тестовая инфраструктура и покрытие — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient.Test\TestEnvironment.cs` (из этапа 3),
`Tests\Basic.cs`, `Tests\DataQuery.cs`, `Tests\Connection.cs`, `Tests\DataProvider.cs`,
`Tests\DbProviderFactory.cs` (переписаны), `Tests\Unit\CommandUnit.cs` (новый),
`Tests\Python\CommandTests.cs` (новый), `Tests\Python\ColumnTypesTests.cs` (новый),
`app.config`, `SettingsMain.settings`, `Pike.PythonClient.Test.csproj`

**Сделано**
- Убраны «личные» пути (`C:\Users\Pike\anaconda3\...`) из `app.config` и `SettingsMain.settings`; значение
  в настройках пустое, путь берётся из `PYTHONNET_TEST_DLL`, иначе тест становится `Inconclusive`, а не падает.
- Все интеграционные тесты помечены `[TestCategory("Python")]`, добавлены `[TestInitialize]` (skip) и
  `[TestCleanup]` (`PythonDataProvider.Shutdown`) — движок больше не остаётся в непредсказуемом состоянии.
- Дублирующаяся подготовка (4 копии сборки connection string) заменена на `TestEnvironment.CreateBuilder()` /
  `CreateConnection()`, которые используют публичный `PythonDataProvider.DefaultPathComponents`.
- Ложно-положительный тест `TestInvalidSyntaxInPythonCode` заменён на
  `Assert.ThrowsException<PythonException>(...)`.
- Все легаси-тесты переведены на новый API (`PythonScriptContext`, `ExecuteScript`), поэтому сборка
  больше не содержит предупреждений `CS0618` от устаревших `PythonDataQuery.Query/Parameters/Reset`.
- Новые тесты:
  - `CommandTests` (Python): `SchemaOnly`, `SingleRow`, `CloseConnection`, `ExecuteNonQuery` без `result` и с
    результатом, `CommandTimeout` → `NotSupportedException`, чтение скрипта из файла в UTF-8 (юникод),
    срез `@`/`:` префиксов параметров, передача `decimal`/`Guid`/`TimeSpan`/`DateTime(Utc)`;
  - `ColumnTypesTests` (Python): `int8`, `uint32`, `float32`, `Int64`/`pd.NA`, `boolean`, `string[python]`,
    `timedelta64`, `NaT`, даты до 1970, tz-aware, нормализация «плохих» имён, дубликаты → `NotSupportedException`,
    `complex128` → `NotSupportedException`, отключаемый strict-режим;
  - `CommandUnit` (Unit): контракты команды/соединения без Python.
- `DbProviderFactory`-тесты помечаются `Inconclusive`, если провайдер не зарегистрирован в `machine.config`.

**Проверка**
```
& $msbuild 'PythonClient.sln' /p:Configuration=Debug '/p:Platform=Any CPU' /m   # успешно, предупреждений нет
& $vstest '...Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Unit'    # 85/85 пройдено
& $vstest '...Pike.PythonClient.Test.dll' '/TestCaseFilter:TestCategory=Python'  # 45 тестов пропущено (Inconclusive)
```
Интеграционные тесты (45) требуют Python 3.11 x64 + numpy/pandas и `PYTHONNET_TEST_DLL`; в текущем окружении
они корректно пропускаются, их прогон остаётся для CI/машины с Python.


---

## Этап 6. P2 — сборка, упаковка, документация, CI

### 6.1 P2-1/P2-3: SDK-style проекты — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\Pike.PythonClient64.csproj`, `Pike.PythonClient.Test\Pike.PythonClient.Test.csproj`
(переписаны), `packages.config` удалены (оба проекта)

**Сделано**
- Оба проекта переведены на `Microsoft.NET.Sdk` (`TargetFramework=net48`, `PlatformTarget=x64`,
  `LangVersion=latest`, `GenerateAssemblyInfo=false` — атрибуты остались в `Properties\AssemblyInfo.cs`),
  `AppendTargetFrameworkToOutputPath=false` — пути `bin\Debug|Release` сохранены.
- Зависимость `pythonnet 3.0.5` и `MSTest.TestAdapter`/`MSTest.TestFramework` + `Microsoft.NET.Test.Sdk` оформлены
  через `PackageReference`; `packages.config`, `HintPath` и `EnsureNuGetPackageBuildImports` удалены.
- Основной проект: сохранены сильное имя (`key.snk`), XML-документация (`GenerateDocumentationFile`, `NoWarn 1591`),
  явные ссылки `System.Data`, `System.Xml`, `System.Configuration.Install`, `Microsoft.CSharp`.
- P2-3: удалён `PostBuildEvent` (удаление `.pdb`), для Release оставлены portable PDB
  (`DebugType=portable`) — символы нужны для диагностики в production.
- Тестовый проект теперь тоже `x64`, поэтому предупреждение `MSB3270` (AnyCPU → AMD64) исчезло.

**Проверка**
```
dotnet build PythonClient.sln -c Debug    # успешно, 0 предупреждений, 0 ошибок
dotnet build PythonClient.sln -c Release  # успешно, 0 предупреждений
msbuild PythonClient.sln /restore /p:Configuration=Release   # legacy-путь MSBuild тоже работает
vstest --TestCaseFilter:TestCategory=Unit  # 85/85
```
Артефакты `bin\Release`: `Pike.PythonClient64.dll`, `Pike.PythonClient64.pdb` (больше не удаляется),
`Pike.PythonClient64.xml`, `Python.Runtime.dll`. Предупреждений `NU1701` нет.


### 6.2 P2-2/P2-5: `install.ps1` / `uninstall.ps1` вместо `.vdproj` — ✅ (2026-10-29)

**Изменённые файлы:** `install.ps1`, `uninstall.ps1` (новые)

**Сделано**
- `install.ps1`: копирует `Pike.PythonClient64.dll/.xml`, `Python.Runtime.dll/.xml` в
  `%ProgramFiles%\Pike\PythonClient64` (параметр `-InstallPath`) и регистрирует провайдер в x64 `machine.config`
  через публичный `DbProviderInstallation.RegisterProvider` (бэкап и атомарная запись — из P0-5).
  Проверяет права администратора, поддерживает `-Configuration` и `-SkipMachineConfig`.
- `uninstall.ps1`: снимает запись из `machine.config` (`UnregisterProvider`) и удаляет установленные файлы и
  каталог (если он опустел).
- Зависимость рантайм-сборки от `Microsoft.Build.Utilities` убрана ещё в P0-5, поэтому для регистрации больше не
  нужен MSBuild. `.vdproj` остаётся в репозитории как устаревший вариант (требует расширения VS Installer Projects);
  рекомендуемый путь — скрипты (зафиксировано в README и CHANGELOG).

**Проверка**
- Скрипты проверены статически (сборка не требуется); регистрация провайдера покрыта Unit-тестами
  `DataProviderInstallationUnit` (9 тестов, включая бэкап, обновление записи и unregister).
- Ручная проверка `install.ps1 -SkipMachineConfig` на этом окружении не выполнялась: требуется Python/Excel-сценарий;
  скрипт не изменяет систему без явного запуска.

### 6.3 P2-6: качество кода `SupportedTypes` / `KeyIndexCollection` — ✅ (2026-10-29)

Сделано в рамках шагов 1.2, 2.2 и 4.1 (реестр конвертеров, `Set/Insert/TryGetValue/RenameKey`, упорядоченные
`Values`/перечисление, потокобезопасная инициализация).

### 6.4 P2-4/P2-5 + раздел 8: документация — ✅ (2026-10-29)

**Изменённые файлы:** `README.md` (переписан), `CHANGELOG.md`, `LICENSE`, `SECURITY.md`, `.editorconfig`,
`docs/connection-strings.md`, `docs/power-query.md`

**Сделано**
- `README.md`: требования (.NET 4.8 x64, pythonnet 3.0.5, Python 3.7–3.13 x64, numpy/pandas, 64-bit Excel),
  установка через скрипты и ручная регистрация, self-contained quick start (без `SettingsMain` из тестового проекта),
  источники скрипта, все ключи connection string с квотированием, таблица параметров и типов колонок, политика
  `NaN`/`NaT`/`None` → `DBNull`, ограничения (один движок, нет транзакций/таймаута, threading, память),
  Power Query, troubleshooting-таблица, сборка/тесты, раздел безопасности, опечатки («Quik», «Commpress») исправлены.
- `CHANGELOG.md` (Keep a Changelog) с разделами Fixed/Added/Changed/Deprecated/Breaking changes и инструкцией миграции
  по квотированию `PYTHONPATH`.
- `LICENSE` (MIT), `SECURITY.md` (модель угроз, политика раскрытия, статус `key.snk`),
  `.editorconfig` (UTF-8, отступы, окончания строк), `docs/connection-strings.md`, `docs/power-query.md`.

### 6.5 CI — ✅ (2026-10-29)

**Изменённые файлы:** `.github/workflows/ci.yml` (новый)

**Сделано**
- GitHub Actions (`windows-latest`): setup-dotnet 8, setup-python 3.11 x64 + `numpy`/`pandas`, `dotnet restore`,
  `dotnet build -c Release`, быстрый набор `TestCategory=Unit`, интеграционный набор `TestCategory=Python`
  с `PYTHONNET_TEST_DLL`, публикация артефактов (dll/xml/pdb/`Python.Runtime.dll`).

**Проверка**
- Локально повторены шаги сборки и обоих фильтров тестов (Unit 85/85, Python — 45 пропущено без Python).
  Прогон workflow возможен только в GitHub после push.


---

## Этап 7. Валидация на реальном Python (после P0/P1/P2)

### 7.1 Локальное окружение Python через `uv` — ✅ (2026-10-29)

**Сделано**
- `uv venv .venv --python 3.11` → CPython **3.11.16 x64** (uv-managed), `uv pip install numpy pandas` →
  **numpy 2.4.6**, **pandas 3.1.0**. Каталог `.venv` добавлен в `.gitignore`.
- `PYTHONNET_TEST_DLL = <uv>\cpython-3.11-windows-x86_64-none\python311.dll`,
  `PYTHONPATH = <repo>\.venv\Lib\site-packages` (провайдер добавляет `PYTHONPATH` из окружения в `sys.path`).
- Скрипты окружения/прогона перенесены в репозиторий: `scripts\test\Initialize-PythonEnvironment.ps1`,
  `scripts\test\Invoke-Tests.ps1` (таймаут + разбор trx), `scripts\smoke\Test-ProviderSmoke.ps1`;
  `install.ps1`/`uninstall.ps1` перенесены в `scripts\`.

### 7.2 Дефекты, найденные интеграционным прогоном (и исправленные) — ✅ (2026-10-29)

| # | Симптом | Причина | Исправление |
|---|---|---|---|
| 1 | Вызов Python из другого потока зависал навсегда (`Py.GIL()` не возвращался), `Parallel.For` — deadlock | `PythonEngine.Initialize()` оставляет GIL за потоком инициализации | После `Initialize()` вызывается `PythonEngine.BeginAllowThreads()`, состояние потока сохраняется и восстанавливается (`EndAllowThreads`) перед `Shutdown`; добавлен регрессионный тест `P0_6_GilIsAvailableFromAnotherThread` |
| 2 | `1960-01-01` возвращался как `1969-12-28 08:19:40`, `timedelta 1h` как `3.6 s` | pandas 3 использует `datetime64[us]`/`timedelta64[us]`, конвертеры жёстко считали наносекунды | Разрешение берётся из dtype (`GetDtypeUnit`/`GetNanosecondsPerUnit`) и значения масштабируются к нс до конвертации (`ScaleToNanoseconds`, с проверкой переполнения и сохранением `NaT`) |
| 3 | `PythonException: KeyError '0'` для DataFrame с колонками по умолчанию | после перевода имён в строки серия выбиралась по строке, а метки были числами | серия выбирается по исходной метке (`PyObject`), строка используется только для имени `DataColumn` |
| 4 | `pd.NA` в `string`/`object`-колонках попадал в `DataTable` строкой `"<NA>"` | `pd.NA` не является `None` | `PythonValue.IsNone` распознаёт `pd.NA`/`pd.NaT` (стабильный repr) → `DBNull` |
| 5 | `PythonEngine.Shutdown()` вызывался под `lock` провайдера | риск взаимной блокировки с потоком, держащим GIL | `Shutdown` вынесен за пределы `lock`, `Close()` идемпотентен, добавлен `PythonDataProvider.ReferenceCount` |
| 6 | Тесты зависали/падали при повторной инициализации интерпретатора с numpy/pandas | перезапуск движка между тестами + несбалансированные ссылки | `TestEnvironment` держит движок живым (`ShutdownOnLastClose = false`), `[AssemblyCleanup]` → `Shutdown()`; `[ClassCleanup]` в этом окружении не вызывается, поэтому используется идемпотентный `EnsureProviderOpen()`; тесты проверяют переходы `ReferenceCount`, а не глобальный `State` |

Также в тестовом `app.config` добавлена регистрация провайдера (`system.data/DbProviderFactories`), поэтому
`DbProviderFactories`-тесты проходят без изменения `machine.config` и без прав администратора.

### 7.3 Итоговый прогон — ✅ (2026-10-29)

```
dotnet build PythonClient.sln -c Debug                     # 0 предупреждений, 0 ошибок
.\scripts\test\Invoke-Tests.ps1 -Category All \
    -PythonDll <uv>\python311.dll -SitePackages <repo>\.venv\Lib\site-packages
# Total: 131, Passed: 131, Failed: 0, Skipped/Inconclusive: 0
.\scripts\smoke\Test-ProviderSmoke.ps1                     # All 40 smoke checks passed
```

Интеграционный набор покрывает: GIL/refcount (включая вызов из другого потока и `Parallel.For`), параметры
(`null`, `decimal`, `Guid`, `TimeSpan`, `DateTime(Utc)`, префиксы `@`/`:`), `CommandBehavior`
(`SchemaOnly`, `SingleRow`, `CloseConnection`), `CommandTimeout`, скрипт из файла в UTF-8, все поддерживаемые dtype
(`int8`/`uint32`/`float32`/`Int64`/`boolean`/`string`/`category`/`datetime64`/`timedelta64`), `NaT`/`NaN`/`pd.NA`,
даты до 1970 и tz-aware колонки, «плохие» и дублирующиеся имена колонок, `complex128` и strict-режим.


---

## Этап 8. Матрица версий Python / pandas

### 8.1 Инфраструктура матрицы — ✅ (2026-10-29)

**Изменённые файлы:** `scripts\test\PythonEnvironment.ps1` (новый), `scripts\test\Initialize-PythonEnvironment.ps1`
(переведён на общие функции), `scripts\test\Invoke-VersionMatrix.ps1` (новый), `.gitignore` (`.venv-matrix/`)

**Сделано**
- Общий модуль `PythonEnvironment.ps1`: `New-PythonEnvironment` (создание venv через `uv` + установка пакетов),
  `Get-PythonRuntimeInfo` (версия, путь к `python3xx.dll`, `site-packages`), `Set-PythonTestEnvironment`
  (выставляет `PYTHONNET_TEST_DLL`/`PYTHONPATH`).
- `Invoke-VersionMatrix.ps1`: каждая комбинация получает отдельное окружение в `.venv-matrix\<name>`,
  сборка выполняется один раз, затем для каждой комбинации прогоняется набор `TestCategory=Python` с таймаутом;
  в конце печатается сводная таблица и скрипт падает только при провале *поддерживаемой* комбинации.

### 8.2 Результаты матрицы — ✅ (2026-10-29)

| Python | pandas | numpy | Результат |
|---|---|---|---|
| 3.9.25 | 1.5.3 | 1.26.4 | **46 / 46** |
| 3.11.16 | 2.2.3 | 1.26.4 | **46 / 46** |
| 3.11.16 | 3.1.0 | 2.4.6 | **46 / 46** |
| 3.13.15 | 3.1.0 | 2.5.4 | **46 / 46** |

Выводы:
- регрессий между pandas 1.5 / 2.2 / 3.1 нет — обработка разрешения `datetime64`/`timedelta64` (ns/us) и `pd.NA`/`NaT`
  корректна на всех версиях;
- Python 3.13 работает с pythonnet 3.0.5 (проверено на всём наборе), поэтому комбинация переведена в
  «поддерживаемые»; 3.7/3.8 не проверялись;
- базовый замер производительности: `TestLargeDataFrame` (90 000 строк × 6 колонок) — **~0.3 с** на прогон
  (Debug, «тёплый» движок), то есть текущая реализация конвертации не является узким местом.

**Проверка**
```
.\scripts\test\Invoke-VersionMatrix.ps1                     # 4 комбинации, сводная таблица
.\scripts\test\Invoke-Tests.ps1 -Category All               # 131 / 131 (Unit 85 + Python 46)
```


---

## Этап 9. Релизная инженерия: версия 2.0.0 и NuGet-пакет

### 9.1 Общие свойства сборки — ✅ (2026-10-29)

**Изменённые файлы:** `Directory.Build.props` (новый), `Pike.PythonClient64\Properties\AssemblyInfo.cs`,
`Pike.PythonClient64\Pike.PythonClient64.csproj`, `Pike.PythonClient.Test\Pike.PythonClient.Test.csproj`,
`Pike.PythonClient.Test\Properties\AssemblyInfo.cs` (удалён)

**Сделано**
- Единая точка версии/метаданных: `Directory.Build.props` (`Version`/`AssemblyVersion`/`FileVersion` = **2.0.0**,
  `Company`, `Product`, `Authors`, `Copyright`, `Description`, `LangVersion`, `NeutralLanguage`,
  `PackageLicenseExpression=MIT`, `RepositoryType`, `PackageTags`, `PackageReadmeFile`).
- Атрибуты сборки генерируются SDK (удалён `GenerateAssemblyInfo=false`); в `AssemblyInfo.cs` остались только
  `ComVisible(false)` и `Guid` (нужны для COM/typelib), в тестовом проекте файл удалён.
- Версия подтверждена у собранной сборки: `FileVersion = 2.0.0.0`, `ProductVersion = 2.0.0+<sha>`.

### 9.2 NuGet-пакет и артефакты — ✅ (2026-10-29)

**Изменённые файлы:** `Pike.PythonClient64\Pike.PythonClient64.csproj`, `scripts\pack.ps1` (новый), `.gitignore`

**Сделано**
- `PackageId = Pike.PythonClient64`, `Title`, `IncludeSymbols`/`SymbolPackageFormat = snupkg`, README включается
  в пакет (`PackagePath="\"`), `artifacts/` добавлен в `.gitignore`.
- `scripts\pack.ps1`: Release-сборка → `dotnet pack` в `artifacts\` + «плоская» папка
  `artifacts\Pike.PythonClient64` (`dll/xml/pdb` + `Python.Runtime.dll`) для `scripts\install.ps1`
  и ручной поставки в Excel/Power Query.

**Проверка**
```
.\scripts\pack.ps1
# artifacts\Pike.PythonClient64.2.0.0.nupkg      (43 023 bytes)
# artifacts\Pike.PythonClient64.2.0.0.snupkg     (13 374 bytes)
# artifacts\Pike.PythonClient64\{dll,xml,pdb,Python.Runtime.dll}
```
Содержимое пакета: `lib\net48\Pike.PythonClient64.dll`, `lib\net48\Pike.PythonClient64.xml`, `README.md`,
`nuspec` с зависимостью `pythonnet 3.0.5`, framework-сборками (`System.Data`, `System.Xml`,
`System.Configuration.Install`, `Microsoft.CSharp`) и лицензией MIT.

**Регрессии:** `Invoke-Tests.ps1 -Category All -Configuration Release` → **131 / 131 пройдено**
после переноса метаданных и удаления `AssemblyInfo.cs`.


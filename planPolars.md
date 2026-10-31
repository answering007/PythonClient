# planPolars.md — поддержка polars без конвертации в pandas

> Дата: 2026-11-01 · Объект: `Pike.PythonClient64` **2.1.0** · pythonnet **3.0.5** · .NET Framework **4.8** / x64
> Роль: план реализации (не ревью)
> Статус: **реализовано** — этапы 1–6, результат и отклонения от плана см. [§10](#10-результат-реализации)
> Решения: Q1 — автодетект (согласован), Q2–Q12 — приняты при реализации; изменение по Q8 зафиксировано в §9.

## Содержание

- [0. Цель и границы](#0-цель-и-границы)
- [1. Как есть сейчас](#1-как-есть-сейчас)
- [2. Архитектурное решение](#2-архитектурное-решение)
- [3. Чтение polars: технические детали](#3-чтение-polars-технические-детали)
- [4. Соответствие типов](#4-соответствие-типов)
- [5. Пошаговый план работ](#5-пошаговый-план-работ)
- [6. Тест-план](#6-тест-план)
- [7. Документация и артефакты](#7-документация-и-артефакты)
- [8. Риски и Definition of Done](#8-риски-и-definition-of-done)
- [9. Принятые и открытые решения](#9-принятые-и-открытые-решения)
- [10. Результат реализации](#10-результат-реализации)
- [Приложение A. Эскизы кода](#приложение-a-эскизы-кода)
- [Приложение B. Порядок выполнения (чек-лист)](#приложение-b-порядок-выполнения-чек-лист)

---

## 0. Цель и границы

**Цель.** Скрипт может присвоить `result` объект `polars.DataFrame`; провайдер сам превращает его в
`DataTable` / `DbDataReader`, **без** `df.to_pandas()` в пользовательском скрипте. Поведение для Power Query /
Excel, параметров (`params`), `query`, `FILE=`, async-API и `CommandBehavior` не меняется.

Сценарий-триггер:

```python
import polars as pl

result = pl.DataFrame({
    'No': [1, 2, 3],
    'Name': ['Olivia', 'Ava', 'Isabella'],
    'Value': [0.0, 0.6931471805599453, 1.0986122886681098],
})
```

**Не входит в задачу:**

- `pyarrow.Table` и arrow-протокол (возможное расширение позже, архитектура `IFrameReader` его допускает);
- стриминговое / ленивое чтение: кадр по-прежнему материализуется целиком
  (`PythonDataQuery.ConvertResultToDataTable`);
- передача polars-объектов **в** скрипт через `params` (там остаются .NET-значения, см. `ToPythonValue`);
- кэширование кадров между вызовами, поддержка `polars.SQLContext` и прочих API polars.

**Ограничение по умолчанию:** polars остаётся **опциональным** python-пакетом. Провайдер не импортирует его,
не зависит от его версии на этапе сборки и не должен падать, если polars не установлен (pandas-сценарий
работает как раньше).

---

## 1. Как есть сейчас

| Место | Роль |
|---|---|
| `PythonDataQuery.cs:60` `RunScript` | контекст → `ExecuteScript` → `ConvertResultToDataTable` |
| `PythonDataQuery.cs:89` `ExecuteScript` | `Py.CreateScope()`, кладёт `query`/`params`, выполняет скрипт, забирает переменную `result` |
| `PythonDataQuery.cs:342` `ConvertResultToDataTable` | `using (Py.GIL())` вокруг чтения + `Dispose` кадра после чтения (владение) |
| `PythonDataQuery.cs:237` `ReadDataFrame` | **pandas-специфика**: `columns.tolist()`, `df[label]` → `Series`, `series.dtype`, проверка дублей |
| `PythonDataQuery.cs:402` `ReadColumnValues` | спец-путь datetime/timedelta: `series.astype('int64')` + масштабирование в нс |
| `PythonDataQuery.cs:443/471/489` | `ScaleToNanoseconds`, `GetDtypeUnit` (парсит `[ns]`), `GetNanosecondsPerUnit` |
| `PythonDataQuery.cs:381/306/364` | `MakeColumnName` (`.`/`[`/`]`), `FillRows` (`BeginLoadData`), strict-политика |
| `SupportedTypes.cs:29` `NormalizeDtype` | срезает байтовый порядок (`<i8`) и всё начиная с `[` (`datetime64[ns, UTC]` → `datetime64`) |
| `SupportedTypes.cs:117` `CreateDefault` | реестр dtype → `IColumnConverter` (case-insensitive) |
| `ColumnConverters/*` | `bool` / `DateTime` (нс от 1970) / `double` / `int` / `long` / `string` / `TimeSpan` (нс) |
| `Data/PythonCommand.cs:143` `ExecuteDbDataReader` | ADO.NET-слой: `SchemaOnly`, `SingleRow`, `ExecuteScalar`, `ExecuteNonQuery` — **от формата кадра не зависит** |

Выводы, на которых построен план:

1. **Точка расширения одна** — `PythonDataQuery.ReadDataFrame`. ADO.NET-слой (`PythonCommand`), передача
   `query`/`params`, GIL/`Dispose`, `FillRows`, нормализация имён колонок переиспользуются как есть.
2. Проверка «это DataFrame?» (`:239`) — duck-typing по наличию `columns` + `dtypes`. **У `polars.DataFrame`
   оба атрибута тоже есть**, поэтому сегодня polars-результат попадает в pandas-путь и падает на
   `series.values` (`AttributeError`). Значит, детект по типу объекта обязателен (совпадает с решением Q1).
3. `NormalizeDtype` не понимает polars-репризентацию dtype: `Datetime(time_unit='us', time_zone=None)`
   режется только по `[`, поэтому в реестр попадёт строка с параметрами. Нужен срез и по `(`.
4. polars **не установлен** ни в `.venv`, ни в `.venv-matrix`, ни в CI (`ci.yml:28` — `numpy pandas`).
   Значит, polars-тесты обязаны быть `Inconclusive` без пакета, а CI должен ставить polars явно.

## 2. Архитектурное решение

Вводим внутренний слой «ридер кадра»; **публичный API не меняется** (Q1: автодетект, без `FRAME=` и без
`PythonScriptContext.FrameKind`):

```
Pike.PythonClient64/FrameReaders/            (internal)
├── IFrameReader.cs          bool CanRead(PyObject result);
│                            DataTable Read(PyObject result, string tableName);
│                            string Kind;                    // "pandas" | "polars" (диагностика)
├── FrameReaderFactory.cs    детект по __module__/__name__ без import pandas/polars
├── PandasFrameReader.cs     перенос текущей логики 1:1 (регресс-паритет)
├── PolarsFrameReader.cs     новая логика чтения
├── FrameReaderBase.cs       MakeColumnName, FillRows, GetColumnConverter, AddColumn
└── DtypeUnits.cs            единицы: pandas "datetime64[ns]" и polars "Datetime(time_unit='us')"
```

`PythonDataQuery` после рефакторинга (минимальная правка):

```csharp
public static DataTable RunScript(string scriptText, PythonScriptContext context)
{
    var executionContext = ResolveContext(context);
    var result = ExecuteScript(scriptText, executionContext, out var resultName);
    if (result == null) return new DataTable(resultName);

    // ConvertResultToDataTable по-прежнему владеет объектом, держит GIL и делает Dispose
    return ConvertResultToDataTable(result, resultName);
}

static DataTable ConvertResultToDataTable(PyObject frame, string resultName)
{
    try
    {
        using (Py.GIL())
            return FrameReaderFactory.Read(frame, resultName);   // вместо ReadDataFrame
    }
    finally
    {
        using (Py.GIL())
            frame.Dispose();
    }
}
```

### 2.1. Детект формата (Q1)

Никаких `import pandas` / `import polars` — только строки типа объекта:

```csharp
readonly struct PythonTypeInfo
{
    public string Module { get; }   // "pandas.core.frame" | "polars.dataframe.frame"
    public string Name { get; }     // "DataFrame" | "LazyFrame"

    public static PythonTypeInfo From(PyObject value)
    {
        using (PyObject type = value.GetPythonType())
        using (PyObject module = type.GetAttr("__module__"))
        using (PyObject name = type.GetAttr("__name__"))
            return new PythonTypeInfo(module.ToString(), name.ToString());
    }
}
```

Маршрутизация в `FrameReaderFactory.Read`:

| Условие | Действие |
|---|---|
| `Module` начинается с `polars` и `Name == "DataFrame"` | `PolarsFrameReader.Read` |
| `Module` начинается с `polars` и `Name == "LazyFrame"` | `InvalidOperationException` с подсказкой `result = <lazy>.collect()` (Q2) |
| `Module` начинается с `polars` (прочее: `Series`, `Expr`) | `InvalidOperationException` с фактическим типом |
| `Module` начинается с `pandas` и `Name == "DataFrame"` | `PandasFrameReader.Read` |
| иначе: у объекта есть `columns` **и** `dtypes` | `PandasFrameReader.Read` — обратная совместимость (подклассы pandas, `geopandas`, `modin`, test doubles) |
| иначе | `InvalidOperationException`: `'result' must be a pandas.DataFrame or polars.DataFrame, got <type>` |

Важно: строки модуля — единственный контракт детекта; они стабильны в polars 0.19–1.x и pandas 1.x–3.x.
Никакого «пробного» вызова методов у чужого объекта до выбора ридера (иначе получим неинформативные
`AttributeError`).

### 2.2. Расширение реестра `SupportedTypes`

`NormalizeDtype` (`SupportedTypes.cs:29`) дополняется срезом параметров в круглых скобках:

```csharp
// polars: Datetime(time_unit='us', time_zone=None) → Datetime, Decimal(precision=38, scale=2) → Decimal,
// List(String) → List, Enum(categories=['a', 'b']) → Enum
var parenthesis = normalized.IndexOf('(');
if (parenthesis > 0) normalized = normalized.Substring(0, parenthesis);
```

Порядок важен: сначала `[`, затем `(` (pandas-кейсы `datetime64[ns, UTC]`, `string[python]`, `<i8`
остаются без изменений — это фиксируется unit-тестами).

Новые ключи реестра (регистр не важен, поэтому `Boolean`/`Int64`/`Float64`/`String` уже покрыты pandas-именами):

| Ключ | Конвертер |
|---|---|
| `Float16`, `Utf8` (алиас `String`), `Date`, `Datetime`, `Duration`, `Time` | см. §4 |
| `Categorical`, `Enum` | `StringConverter` |
| `Decimal`, `Binary`, `Null` | новые конвертеры (по §9) |
| `Int128`, `UInt128`, `Object`, `Unknown`, `List`, `Array`, `Struct`, `Map`, `Extension` | «запрещённые» имена: strict → `NotSupportedException`, non-strict → `StringConverter` |

Проверку запрещённых имён делает общий `FrameReaderBase.GetColumnConverter(name, dtype, isForbidden)`,
чтобы сообщение об ошибке совпадало у pandas и polars: `Column '<name>' has unsupported dtype '<dtype>'`.

## 3. Чтение polars: технические детали

### 3.1. Метаданные кадра

```csharp
using (PyObject height = frame.GetAttr("height"))          // rows
using (PyObject columns = frame.GetAttr("columns"))        // list[str], уникальные, всегда строки
using (PyObject dtypes = frame.GetAttr("dtypes"))          // list[DataType]
using (PyObject series = frame.InvokeMethod("get_column", new PyString(name)))
using (PyObject dtype = series.GetAttr("dtype"))           // str(dtype) → "Datetime(time_unit='us', time_zone=None)"
```

Колонки читаем **по имени** через `get_column(name)` (не `frame[name]`): это документированный API, не
зависящий от `__getitem__`-сахара. Имена уникальны по контракту polars, но проверку дублей и нормализацию
(`MakeColumnName`) оставляем — это защита от нестандартных расширений и от `.`/`[`/`]` в именах.

### 3.2. Извлечение значений

| Группа dtype | Способ | Почему |
|---|---|---|
| `Boolean`, `Int8..Int64`, `UInt8..UInt64`, `Float16/32/64`, `String/Utf8`, `Categorical`, `Enum`, `Null`, `Binary` | `series.to_list()` → `(object[])(dynamic)` (тот же приём, что в `ConvertSeriesValues`) | значения уже .NET-совместимы: `bool`, `int`, `float`, `str`, `bytes`, `None` |
| `Date`, `Datetime`, `Duration`, `Time` | `series.to_physical()` → масштабирование в наносекунды → существующие `DateTimeConverter` / `TimeSpanConverter` | `to_list()` даёт python `datetime`/`date`/`timedelta`: точность максимум µs, диапазон 1..9999 год, лишние объекты на каждое значение |
| `Decimal` | `to_list()` (python `decimal.Decimal`) | `to_physical()` даёт `Int128`, который .NET-конвертерами не покрыт |
| `Categorical`, `Enum` | **только** `to_list()` | `to_physical()` для них — коды `UInt32`, а не строки (документировано в polars) |
| `List`, `Array`, `Struct`, `Map`, `Object`, `Unknown`, `Extension`, `Int128`, `UInt128` | strict → исключение, non-strict → строка | не имеют однозначного .NET-представления в `DataTable` |

`to_physical()` (по документации polars): `Date` → `Int32` (дни от 1970), `Datetime` → `Int64` в своём
`time_unit`, `Time` → `Int64` (наносекунды от полуночи), `Duration` → `Int64` в своём `time_unit`; null
сохраняются как `None`. Для `Date` масштаб — `86_400_000_000_000` нс в сутках.

### 3.3. Единицы времени (`DtypeUnits`)

```csharp
// pandas: "datetime64[ns]"                     → ns
// polars: "Datetime(time_unit='us', time_zone=None)" → us, time_zone != None → UTC
static string GetPolarsTimeUnit(string dtype)   // содержимое time_unit='...' или "ns" по умолчанию
static bool IsTimeZoneAware(string dtype)       // time_zone=None → false
```

`GetNanosecondsPerUnit` переиспользуется как есть (`ns/us/ms/s/m/h/D`). Если `to_physical()` бросил
`PythonException` (несовместимая версия polars, экзотический extension dtype) — fallback на `to_list()` с
`DateTimeConverter`/`TimeSpanConverter`, которые уже принимают готовые `DateTime`/`TimeSpan` значения.

### 3.4. `null`, `NaN`, `None`

- `None` (polars null) → `DBNull` — через существующий `PythonValue.IsNone`;
- `float('nan')` → **`DBNull`** (предлагаемое решение Q4): единообразие с pandas-путём, где `np.nan` тоже
  считается пропуском и Power Query получает `null`;
- `Date`/`Datetime`/`Time`/`Duration` null → `DBNull` (не путать с 1970-01-01 / нулевым `TimeSpan`);
- переполнение `DateTime`/`TimeSpan` (например, год 12000 после конвертации) → `DBNull`, как в текущих
  конвертерах (`DateTimeConverter.ConvertValue` уже проверяет границы).

### 3.5. Пустые кадры и кадры без колонок

- `height == 0`: схема берётся из `dtypes`, `FillRows` уже корректно выходит при нуле значений;
  `DataTable` получает типизированные колонки — это важно для Power Query (`SchemaOnly`, «пустой результат»);
- `width == 0` (`pl.DataFrame()`): таблица без колонок, `ExecuteScalar` вернёт `null`, `ExecuteNonQuery` — `0`;
- `Null`-колонка (все значения null) → `string` + `DBNull` (предлагаемое решение Q6).

### 3.6. Производительность и GIL

- читаем **по колонкам** (`get_column` + `to_list()`), не `to_dicts()` / `iter_rows()`: построчный обход в
  pythonnet на порядок дороже;
- весь разбор выполняется внутри одного `using (Py.GIL())` в `ConvertResultToDataTable`; ни один
  `PyObject` не хранится после `frame.Dispose()`;
- `DataTable.BeginLoadData/EndLoadData` и переиспользуемый буфер строки — из `FillRows` без изменений;
- ориентир для дымового perf-теста: 100 000 × 5 колонок — не медленнее, чем эквивалент pandas-пути
  (жёсткого SLA нет, но регресс по времени должен быть заметен в отчёте теста).

### 3.7. Сообщения об ошибках

| Ситуация | Исключение |
|---|---|
| результат не pandas/polars DataFrame | `InvalidOperationException: 'result' must be a pandas.DataFrame or polars.DataFrame, got <type>` |
| `LazyFrame` (Q2) | `InvalidOperationException: 'result' is a polars.LazyFrame, materialize it: result = <expr>.collect()` |
| неизвестный/вложенный dtype в strict | `NotSupportedException: Column '<name>' has unsupported dtype '<dtype>'` |
| `ResultRequired == false` и нет `result` | `new DataTable(resultName)` — без изменений |
| `params`/`query` | без изменений (только .NET-значения → python) |

## 4. Соответствие типов

| polars dtype (после `NormalizeDtype`) | .NET (`DataColumn.DataType`) | Конвертер | Примечание |
|---|---|---|---|
| `Boolean` | `bool` | `BooleanConverter` | `null` → `DBNull` |
| `Int8`, `Int16`, `Int32`, `UInt8`, `UInt16`, `UInt32` | `int` | `IntegerConverter` | вне диапазона / `null` → `DBNull` |
| `Int64`, `UInt64` | `long` | `LongConverter` | `UInt64 > long.MaxValue` → `DBNull` (предл. Q11) |
| `Float16`, `Float32`, `Float64` | `double` | `DoubleConverter` | `NaN` → `DBNull` (предл. Q4) |
| `String`, `Utf8`, `Categorical`, `Enum` | `string` | `StringConverter` | `null` → `DBNull`; `Enum`/`Categorical` читаются как строки |
| `Date` | `DateTime` | `DateTimeConverter` | полночь, дни от 1970 → нс |
| `Datetime` (`ns`/`us`/`ms`, tz) | `DateTime` | `DateTimeConverter` | tz-aware: epoch UTC → `Kind = Utc` (предл. Q8) |
| `Duration` | `TimeSpan` | `TimeSpanConverter` | переполнение → `DBNull` |
| `Time` | `TimeSpan` | `TimeSpanConverter` | время суток от полуночи |
| `Decimal` | `double` | `DoubleConverter` | предл. Q3: `double` для паритета с передачей `decimal` в `params` |
| `Binary` | `byte[]` | новый `BinaryConverter` | предл. Q5 |
| `Null` | `string` | `StringConverter` | все значения → `DBNull` |
| `Int128`, `UInt128` | — | — | strict: `NotSupportedException` (предл. Q11) |
| `Object`, `Unknown`, `Extension` | `string` только non-strict | `StringConverter` | в strict — исключение |
| `List`, `Array`, `Struct`, `Map` | `string` только non-strict | `StringConverter` | представление `str(value)`; JSON — альтернатива Q6 |

Правила, которые остаются общими для pandas и polars:

- `PythonDataQuery.StrictColumnTypes` (по умолчанию `true`) — неизвестный dtype → `NotSupportedException`;
  `false` → строковое представление;
- `SupportedTypes.Register(converter, "Datetime", "Duration")` позволяет переопределить любой polars-dtype;
- `MakeColumnName` (`a.b` → `a_b`, `a[b]` → `a(b)`, коллизии `_2`, `_3`, пустое имя → `ColumnN`);
- `DataColumn.AllowDBNull = true` у всех колонок.

---

## 5. Пошаговый план работ

### Этап 1 — рефакторинг pandas-пути без изменения поведения

1. Создать `Pike.PythonClient64/FrameReaders/`: `IFrameReader`, `FrameReaderBase`, `DtypeUnits`,
   `PandasFrameReader`, `FrameReaderFactory`.
2. Перенести из `PythonDataQuery.cs` в `PandasFrameReader`/`FrameReaderBase`:
   `ReadDataFrame` (`:237`), `ReadColumnValues` (`:402`), `ConvertSeriesValues` (`:430`),
   `ScaleToNanoseconds` (`:443`), `GetDtypeUnit` (`:471`), `GetNanosecondsPerUnit` (`:489`),
   `MakeColumnName` (`:381`), `FillRows` (`:306`), `GetColumnConverter` (`:364`).
3. В `PythonDataQuery` оставить публичный контракт (`RunScript`, `RunScriptAsync`, `StrictColumnTypes`,
   `QueryKey`, `ResultKey`, `PythonParametersKey`, `ToPythonParameter`, `ToPythonDictionary`) и
   `ExecuteScript`; чтение кадра — через `FrameReaderFactory.Read`.
4. XML-документацию перенести вместе с методами (в проекте `GenerateDocumentationFile=true`).
5. **Критерий готовности этапа:** `.\scripts\test\Invoke-Tests.ps1 -Category All` → **154/154**
   (90 unit + 64 integration), без правок существующих тестов. Отдельно прогнать
   `-Filter 'FullyQualifiedName~ColumnTypesTests'` и `~SupportedTypesUnit`.

### Этап 2 — каркас polars (базовые типы)

6. `FrameReaderFactory` + `PythonTypeInfo` + `enum FrameKind` (`Unknown`, `Pandas`, `Polars`) + новые
   сообщения об ошибках (§3.7).
7. `PolarsFrameReader`: `height`, `columns`, `get_column`, `dtype`, `to_list()`; поддержка
   `Boolean`/`Int8..64`/`UInt8..64`/`Float16/32/64`/`String`/`Utf8`/`Null`; переиспользование
   `FrameReaderBase` (имена, strict-политика, `FillRows`).
8. Тесты этого этапа (см. §6): `PolarsCoreTypes`, `PolarsEmptyFrame`, `PolarsColumnNames`,
   `PolarsAdoParity`, `PandasAndPolarsInOneProcess`.

### Этап 3 — темпоральные типы и реестр

9. `SupportedTypes.NormalizeDtype`: срез по `(` (§2.2); unit-тесты на pandas-кейсы (без регресса) и
   polars-кейсы.
10. `DtypeUnits`: `time_unit`/`time_zone` из `Datetime(...)`/`Duration(...)`; регистрация ключей
    `Date`, `Datetime`, `Duration`, `Time`, `Categorical`, `Enum`, `Utf8`, `Float16`.
11. `PolarsFrameReader`: `to_physical()` + масштабирование для `Date`/`Datetime`/`Duration`/`Time`,
    tz-aware → `Kind = Utc`, fallback на `to_list()` при `PythonException`.
12. Тесты: `PolarsTemporalTypes` (все `time_unit`, tz, даты до 1970 и «крайние» годы),
    `PolarsCategoricalAndEnum`, `PolarsDtypeUnit` (unit).

### Этап 4 — краевые типы и политики

13. `Decimal`, `Binary` (новый конвертер), `Categorical`/`Enum`, `Int128`/`UInt128`,
    `Object`/`Unknown`/`Extension`, вложенные `List`/`Array`/`Struct`/`Map`, `Null`-колонки.
14. Политики: `NaN` → `DBNull` (Q4), `UInt64` overflow (Q11), non-strict fallback (Q6),
    представление вложенных значений (`str(value)` vs JSON).
15. Тесты: `PolarsDecimalAndBinary`, `PolarsNullVsNan`, `PolarsNestedStrictAndFallback`,
    `PolarsUInt64Overflow`.

### Этап 5 — тесты и окружения

16. `Tests/Unit/PolarsDtypeUnit.cs` (`[TestCategory("Unit")]`, Python не нужен).
17. `TestEnvironment`: `HasPolars` (проверка `import polars` в уже открытом движке) и
    `SkipIfNoPolars()` → `Assert.Inconclusive` с текстом
    `"polars is not installed in the python environment (uv pip install polars)"`.
18. `Tests/Python/PolarsTypesTests.cs` (`[TestCategory("Python")]`) — см. §6.
19. `Pike.PythonClient.Test/Python/TestScript04.py` — polars-версия сценариев `TestScript01/03`
    (типы + `params`/`query` + декодирование Excel-таблицы без pandas). Копируется автоматически
    (`Pike.PythonClient.Test.csproj:30`, `Python\*.py` → `PreserveNewest`).
20. `scripts/test/Initialize-PythonEnvironment.ps1`: пример/параметр с polars
    (`. .\scripts\test\Initialize-PythonEnvironment.ps1 -Packages numpy,pandas,polars`);
    решение «добавить ли polars в дефолт» — Q9.
21. `scripts/test/Invoke-VersionMatrix.ps1`: новая комбинация
    (`py311-polars1`: `numpy`, `pandas`, `polars`; при поддержке 0.20 — `py39-polars020`).
22. `.github/workflows/ci.yml`: `python -m pip install ... numpy pandas polars` (`:28`), отдельный шаг
    «Integration tests (Python + polars)» либо второй прогон `-Category Python` с
    `-EnvironmentPath` polars-окружения; обновить комментарий `# version-matrix` (`:81`).

### Этап 6 — документация и релизные артефакты

23. `README.md`: новый раздел «polars support» рядом с «Supported DataFrame column types» (`:267`),
    таблица соответствия polars-dtype (см. §4), отличия `null`/`NaN`, отсутствие жёсткой зависимости,
    пример `result = pl.DataFrame(...)`; дополнить «Limitations» (`:290`) и «Tested versions» (`:406`),
    обновить счётчики тестов (сейчас 154 = 90 + 64).
24. `docs/power-query.md`: пример Power Query, где таблица Excel приходит в polars
    (`pl.read_json` / `pl.from_dicts`), рядом с pandas-примером (`:74`).
25. `CHANGELOG.md`: `Added` — polars-результат без `to_pandas()`; `Changed` — `NormalizeDtype` теперь
    срезает и `(...)`; при необходимости — «Test suite: N tests».
26. `samples/Pike.PythonClient64.Sample` — второй сценарий (polars) или комментарий в существующем.
27. `planPolars.md` — обновить §9 по мере принятия решений Q2–Q12.

---

## 6. Тест-план

### 6.1. Unit (без Python)

`Tests/Unit/PolarsDtypeUnit.cs`:

| Тест | Проверка |
|---|---|
| `NormalizeDtype_PolarsParametersAreRemoved` | `Datetime(time_unit='us', time_zone=None)` → `Datetime`; `Duration(time_unit='ms')` → `Duration`; `Decimal(precision=38, scale=2)` → `Decimal`; `List(String)` → `List`; `Array(Int64, 3)` → `Array`; `Struct({'a': Int64})` → `Struct`; `Enum(categories=['a', 'b'])` → `Enum` |
| `NormalizeDtype_PandasBehaviourIsPreserved` | `datetime64[ns, UTC]` → `datetime64`; `string[python]` → `string`; `<i8` → `i8`; `|O` → `O`; `Int64` без изменений |
| `Registry_SupportsPolarsDtypes` | `Boolean`, `Int8..Int64`, `UInt8..UInt64`, `Float16/32/64`, `String`, `Utf8`, `Date`, `Datetime`, `Duration`, `Time`, `Categorical`, `Enum`, `Decimal`, `Binary`, `Null` |
| `Registry_ReturnsExpectedPolarsConverters` | `Datetime` → `DateTimeConverter`; `Duration`/`Time` → `TimeSpanConverter`; `Categorical`/`Utf8` → `StringConverter`; `Decimal` → конвертер по Q3 |
| `Registry_ForbiddenPolarsDtypes` | `List`, `Array`, `Struct`, `Map`, `Object`, `Unknown`, `Extension`, `Int128`, `UInt128`: strict → `NotSupportedException`, non-strict → `string` |
| `DtypeUnits_PolarsTimeUnits` | `ns/us/ms` → 1 / 1 000 / 1 000 000 нс; отсутствие `time_unit` → `ns`; `time_zone=None` → не UTC, иначе UTC |
| `BinaryConverter` (если принят Q5) | `byte[]`-значения, `None` → `DBNull` |

### 6.2. Integration (`Tests/Python/PolarsTypesTests.cs`, `[TestCategory("Python")]`)

Каждый тест начинается с `TestEnvironment.SkipIfNoPython(); TestEnvironment.SkipIfNoPolars();`.

| Тест | Содержание |
|---|---|
| `PolarsCoreTypes` | `bool/int8..int64/uint8..uint64/float16..64/string/utf8` + `None`: типы `DataColumn` и значения точные, `None` → `DBNull` |
| `PolarsTemporalTypes` | `pl.Date`, `pl.Datetime('ns'/'us'/'ms')`, `pl.Duration`, `pl.Time`, tz-aware (`time_zone='UTC'`, `'Europe/Berlin'`), даты до 1970, год 9999: точность и `Kind` |
| `PolarsDecimalAndBinary` | `pl.Decimal(38, 2)` → по Q3; `pl.Binary` → по Q5 |
| `PolarsCategoricalAndEnum` | значения — исходные строки, а не физические коды |
| `PolarsNullVsNan` | `pl.Float64` с `None` и `float('nan')`: политика Q4 |
| `PolarsNullColumn` | колонка `pl.Null` → `string`, все значения `DBNull` |
| `PolarsNestedStrictAndFallback` | `List(Int64)`, `Struct`, `Array`: strict → `NotSupportedException` с именем колонки; `StrictColumnTypes = false` → строки |
| `PolarsUInt64Overflow` | `UInt64 > long.MaxValue` → по Q11 |
| `PolarsEmptyFrame` | `height == 0`: колонки и типы сохраняются; `pl.DataFrame()`: 0 колонок, `ExecuteScalar` → `null` |
| `PolarsColumnNames` | `'a.b'`, `'a[b]'`, `''`: нормализация имён и `ColumnN` |
| `PolarsAdoParity` | `ExecuteReader`, `SchemaOnly`, `SingleRow`, `ExecuteScalar`, `ExecuteNonQuery`, `DataTable.Load`, async-методы |
| `PolarsScriptFileAndVariables` | `FILE=TestScript04.py` + `query` (включая gzip+base64 таблицу Excel) + `params` |
| `PolarsLazyFrameIsRejected` | `result = pl.DataFrame(...).lazy()` → `InvalidOperationException` с подсказкой `collect()` (Q2) |
| `PolarsResultMustBeFrame` | `result = [1, 2, 3]` → понятное `InvalidOperationException` (проверка текста) |
| `PandasAndPolarsInOneProcess` | pandas → polars → pandas на одном соединении (детект не «залипает») |
| `PolarsLargeFrame` | 100 000 × 5 (`int_range`, строки, float, дата, bool): выборочные значения + время чтения в отчёт |

### 6.3. Регресс и окружение

- весь текущий набор остаётся зелёным: **154/154** (90 unit + 64 integration) — прогон после каждого этапа;
- без установленного polars набор не должен падать: polars-тесты `Inconclusive`, растёт счётчик
  «Skipped/Inconclusive» в отчёте. В CI polars ставится явно, поэтому зелёный прогон там — реальная проверка;
- **GAC-оговорка** (машина с установленным провайдером): strong-named сборка грузится из GAC раньше папки
  приложения, поэтому локальная сборка проверяется либо `-p:AssemblyVersion=2.0.0.1`, либо переустановкой
  (`scripts\install.ps1` под администратором). `AssemblyHooks` печатает `Provider assembly: <путь>` — по этой
  строке сразу видно, что именно загрузилось.

---

## 7. Документация и артефакты

| Файл | Изменение |
|---|---|
| `README.md:161` «Script sources» | `result` может быть `pandas.DataFrame` **или** `polars.DataFrame`; формат определяется автоматически |
| `README.md:267` «Supported DataFrame column types» | новый раздел «polars support»: таблица §4, `null`→`DBNull`, `NaN`→`DBNull`, `Decimal`→`double`, `Date`/`Time`, вложенные типы, `LazyFrame` |
| `README.md:290` «Limitations» | polars — опциональный пакет; `LazyFrame` нужно материализовать; вложенные типы требуют `StrictColumnTypes = false` или приведения в скрипте |
| `README.md:406` «Tested versions» | строка с polars (+ версия), обновление счётчиков тестов и таблицы матрицы |
| `docs/power-query.md:74` | пример Power Query: таблица Excel → `pl.read_json` / `pl.from_dicts` → `result` |
| `CHANGELOG.md` | `### Added`: polars-результат без `to_pandas()`; `### Changed`: `NormalizeDtype` срезает `(...)`; счётчик тестов |
| `samples/Pike.PythonClient64.Sample` | второй сценарий (polars) — по Q10 |
| `planPolars.md` | этот файл: §9 обновляется по мере принятия Q2–Q12 |

## 8. Риски и Definition of Done

| Риск | Влияние | Митигация |
|---|---|---|
| Жёсткая зависимость от polars | провайдер перестанет работать там, где polars нет | провайдер **не импортирует** polars; детект только по `__module__`/`__name__`; polars-тесты `Inconclusive` без пакета |
| `to_physical()` — «implementation detail» по документации polars | смена представления в новой версии | тесты на все `time_unit`; fallback на `to_list()` при `PythonException`; версия polars фиксируется в матрице |
| pythonnet и `decimal.Decimal` из polars | неверный тип/исключение | на этапе 4 проверить фактический тип после `to_list()`; при неоднозначности — разбор `str(value)` в `DecimalConverter` |
| `Utf8` (polars 0.x) vs `String` (1.x), разный repr dtype | dtype не найден в реестре | регистрируем оба ключа; `NormalizeDtype` срезает и `[...]`, и `(...)` |
| `UInt64 > long.MaxValue` | тихая потеря значения (`DBNull`) | зафиксировать политику (Q11) в README и тесте `PolarsUInt64Overflow` |
| Боксинг `object[]` на каждую колонку | замедление на больших кадрах | по-колоночное чтение, `BeginLoadData`, буфер строки; perf-тест 100k×5; сравнение с pandas-путём |
| GIL / утечки `PyObject` | падение интерпретатора, утечка | чтение в одном `using (Py.GIL())`, `Dispose` кадра в `finally` (существующий код), `using` на каждый `PyObject` |
| Нативные колёса polars в Excel-окружении | «не ставится» у пользователя | README: установка `polars` в то же окружение, что pandas (`uv pip install polars` / `pip install polars`) |
| Ложная «зелень» без polars | polars-путь не проверяется | явная установка polars в CI + отдельный шаг; локально — предупреждение в отчёте теста |
| Кастомный объект с `columns`+`dtypes`, но не pandas | ошибка в pandas-пути | это текущее поведение сохраняется осознанно (обратная совместимость); сообщение об ошибке содержит фактический тип |
| Смешение pandas и polars в одном процессе | «залипание» статического состояния | ридеры stateless; тест `PandasAndPolarsInOneProcess` |

### Definition of Done

1. Скрипт возвращает `polars.DataFrame` → ADO.NET/Power Query получает корректную таблицу; `to_pandas()` не нужен.
2. Публичный API не изменён (кроме добавленных ключей `SupportedTypes` и, при принятии Q5, `BinaryConverter`);
   поведение pandas-пути идентично прежнему (регресс-тесты 154/154 зелёные).
3. Поддержаны: `Boolean`, целые/беззнаковые/float, `String`/`Utf8`, `Categorical`/`Enum`, `Date`, `Datetime`
   (`ns`/`us`/`ms`, tz), `Duration`, `Time`, `Decimal`, `Binary`, `Null`; вложенные/128-битные — понятная ошибка
   в strict-режиме и строка в non-strict.
4. Ошибки для `LazyFrame`, `Series`, «не DataFrame» — с фактическим типом и подсказкой.
5. Тесты: unit (`PolarsDtypeUnit`) + integration (`PolarsTypesTests`), без polars — `Inconclusive`;
   полный набор зелёный с polars (в CI).
6. `README.md`, `docs/power-query.md`, `CHANGELOG.md`, счётчики тестов обновлены; `planPolars.md` актуален.
7. Проверено в Power Query: запрос возвращает polars-кадр, таблица в Excel обновляется (ручной smoke на
   машине с установленным провайдером, см. GAC-оговорку §6.3).

## 9. Принятые и открытые решения

| # | Вопрос | Решение в плане | Статус |
|---|---|---|---|
| Q1 | Как определяется формат результата | **Автодетект по типу объекта** (`__module__`/`__name__`), без `FRAME=` и без нового свойства контекста | **Принято** |
| Q2 | Что принимаем кроме `polars.DataFrame` | Только `DataFrame`. `LazyFrame` → `InvalidOperationException` с подсказкой `result = <lazy>.collect()`; `Series` → исключение с фактическим типом | Принято |
| Q3 | `Decimal` | `double`: реализовано выделенным `DecimalConverter` (python `decimal.Decimal` / текст → `double`), паритет с передачей `decimal` в `params` | Принято |
| Q4 | `NaN` в float | `DBNull` (как `np.nan` в pandas-пути); различие `null`/`NaN` не пробрасывается | Принято |
| Q5 | `Binary` | `byte[]` + новый `BinaryConverter` (`None` → `DBNull`) | Принято |
| Q6 | Вложенные/объектные типы, `Null` | strict → `NotSupportedException`; non-strict → `str(value)`; `Null`-колонка → `string` + `DBNull` | Принято |
| Q7 | `Date`, `Time` | `Date` → `DateTime` 00:00; `Time` → `TimeSpan` | Принято |
| Q8 | tz-aware `Datetime` | epoch UTC → `DateTime`; **`Kind` не пробрасывается**: `DataColumn` типа `DateTime` нормализует его в `Unspecified` (проверено), значение трактуется как UTC-время | Принято с изменением |
| Q9 | Окружения и версии polars | проверено на polars **2.1.0**, целевые — 1.x/2.x; 0.20 — через алиас `Utf8` (не прогонялось). polars ставится в CI и в матрицу явно; дефолт `Initialize-PythonEnvironment.ps1` остаётся `numpy,pandas` | Принято |
| Q10 | Примеры | `TestScript04.py` — обязателен (нужен для `FILE=`-теста); `PowerQuery - 05 - Polars.xlsx` — после основной реализации | Принято |
| Q11 | `UInt64`/128-битные | `UInt64 > long.MaxValue` → `DBNull`; `Int128`/`UInt128` → strict-исключение, non-strict — строка | Принято |
| Q12 | Публичный признак формата | Не добавляем; `FrameKind` — `internal`, диагностика через сообщения об ошибках | Принято |

Что меняется при пересмотре решения:

| Решение | Где отразится |
|---|---|
| Q2 (`LazyFrame`) | `FrameReaderFactory` (добавить ветку `collect()`), §3.1 (владение объектом), тест `PolarsLazyFrameIsRejected` |
| Q3 (`Decimal`) | новый `DecimalConverter` + регистрация ключа `Decimal`, тест `PolarsDecimalAndBinary`, §4 |
| Q4 (`NaN`) | `PolarsFrameReader` (не считать `NaN` пропуском) + тест `PolarsNullVsNan`; расхождение с pandas-путём задокументировать |
| Q5 (`Binary`) | `BinaryConverter`, регистрация `Binary`, §2.2 |
| Q6 (`str` vs JSON) | `PolarsFrameReader.ReadNestedColumn` + тест `PolarsNestedStrictAndFallback` |
| Q7/Q8 | `PolarsFrameReader.ReadTemporalColumn` (+ `DateTime.SpecifyKind`), тест `PolarsTemporalTypes` |
| Q9 | `ci.yml`, `Invoke-VersionMatrix.ps1`, `Initialize-PythonEnvironment.ps1`, README «Tested versions» |
| Q10 | `Pike.PythonClient.Test\\Excel`, `docs/power-query.md` |
| Q11 | `LongConverter`/`PolarsFrameReader`, тесты `PolarsUInt64Overflow` |
| Q12 | новый публичный член (breaking по смыслу) — только если появится реальная потребность |

---

## 10. Результат реализации

Реализовано 2026-11-01: `dotnet build` (Debug/Release) без ошибок, весь набор тестов — **176/176**
(95 unit + 81 integration, включая 17 polars-тестов).

### 10.1. Изменённые и новые файлы

| Файл | Что сделано |
|---|---|
| `Pike.PythonClient64/FrameReaders/IFrameReader.cs` | контракт ридера кадра |
| `FrameReaders/FrameKind.cs` | internal-признак формата (диагностика) |
| `FrameReaders/PythonTypeInfo.cs` | определение типа по `__module__`/`__name__`, без импорта pandas/polars |
| `FrameReaders/FrameReaderFactory.cs` | маршрутизация и сообщение для «не кадра» |
| `FrameReaders/FrameReaderBase.cs` | общие `MakeColumnName`, `FillRows`, `ScaleToNanoseconds`, strict-политика |
| `FrameReaders/PandasFrameReader.cs` | прежняя логика, перенесённая 1:1 |
| `FrameReaders/PolarsFrameReader.cs` | чтение polars: `to_list()`/`to_physical()`, ошибки `LazyFrame`/`Series` |
| `FrameReaders/DtypeUnits.cs`, `FrameReaders/PolarsDtypes.cs` | единицы времени (`[ns]`/`time_unit='us'`), запрещённые dtype |
| `ColumnConverters/BinaryConverter.cs`, `ColumnConverters/DecimalConverter.cs` | новые конвертеры |
| `SupportedTypes.cs` | срез параметров `(...)` в `NormalizeDtype`, polars-ключи реестра |
| `PythonDataQuery.cs` | делегирование в `FrameReaderFactory`, публичный API без изменений |
| `Pike.PythonClient.Test/Tests/Unit/PolarsDtypeUnit.cs` | 5 unit-тестов (нормализация, реестр, `BinaryConverter`) |
| `Pike.PythonClient.Test/Tests/Python/PolarsTypesTests.cs` | 17 интеграционных тестов |
| `Pike.PythonClient.Test/Python/TestScript04.py` | polars-скрипт: типы + `FILE=`/`query`/`params` + Excel-payload |
| `Pike.PythonClient.Test/TestEnvironment.cs` | `HasPolars`, `SkipIfNoPolars` |
| `scripts/test/Initialize-PythonEnvironment.ps1`, `Invoke-VersionMatrix.ps1`, `.github/workflows/ci.yml` | polars в примерах, матрице и CI |
| `README.md`, `docs/power-query.md`, `CHANGELOG.md` | документация, таблица типов, счётчики тестов |

### 10.2. Отклонения от плана

- **Q8:** `DateTime.Kind` не пробрасывается — `DataColumn` типа `DateTime` нормализует Kind в `Unspecified`
  (проверено отдельным экспериментом). Значение tz-aware `Datetime` — UTC-время; ограничение задокументировано
  в `README.md`.
- **Q3:** вместо регистрации `Decimal` на `DoubleConverter` добавлены выделенные `DecimalConverter` (python
  `decimal.Decimal`/текст → `double`) и `BinaryConverter`.
- **Unit-тесты `DtypeUnits`:** `FrameReaders` намеренно `internal` и без `InternalsVisibleTo` (провайдер
  strong-named, тестовая сборка — нет), поэтому `DtypeUnits`/`PolarsDtypes` покрыты интеграционными тестами через
  реальные polars-dtypes, а `NormalizeDtype`/реестр — unit-тестами.
- **polars 2.1.0** проверен фактически (локальное окружение). Совместимость с 1.x/0.20 обеспечивается алиасом
  `Utf8` и fallback на `to_list()`, отдельным прогоном матрицы не подтверждалась.
- **Отложено:** `PowerQuery - 05 - Polars.xlsx` и polars-сценарий в `samples` (вне DoD этой итерации).

### 10.3. Как проверялось

- `dotnet build PythonClient.sln -c Debug|Release` — без ошибок и предупреждений компилятора;
- `Invoke-Tests.ps1 -Category All`: **176/176** на локальной сборке провайдера;
- на машине с установленным провайдером GAC-копия перекрывает `bin`, поэтому локальная проверка выполнялась
  сборкой с `-p:AssemblyVersion=2.0.0.1` и версией `2.0.0.1` в `bin`-конфиге теста; `AssemblyHooks` печатает
  `Provider assembly: <путь>` — по этой строке видно, какая копия загружена;
- без polars в окружении 17 polars-тестов переходят в `Inconclusive` (проверка `HasPolars`).

---

## Приложение A. Эскизы кода

Эскизы иллюстрируют решения §2–§3; имена и XML-доки финализируются на этапе реализации.

### A.1. Детект и фабрика

```csharp
// Pike.PythonClient64/FrameReaders/FrameReaderFactory.cs
internal enum FrameKind { Unknown, Pandas, Polars }

internal readonly struct PythonTypeInfo
{
    public string Module { get; }
    public string Name { get; }

    PythonTypeInfo(string module, string name) { Module = module; Name = name; }

    public bool IsPolars => Module.StartsWith("polars", StringComparison.Ordinal);
    public bool IsPandas => Module.StartsWith("pandas", StringComparison.Ordinal);

    public static PythonTypeInfo From(PyObject value)
    {
        using (PyObject type = value.GetPythonType())
        using (PyObject module = type.GetAttr("__module__"))
        using (PyObject name = type.GetAttr("__name__"))
            return new PythonTypeInfo(module.ToString(), name.ToString());
    }

    public override string ToString() => $"{Module}.{Name}";
}

internal static class FrameReaderFactory
{
    static readonly IFrameReader Polars = new PolarsFrameReader();
    static readonly IFrameReader Pandas = new PandasFrameReader();

    /// <summary>Reads the script result. Must be called with the GIL acquired</summary>
    public static DataTable Read(PyObject frame, string tableName)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));

        var type = PythonTypeInfo.From(frame);
        if (type.IsPolars) return Polars.Read(frame, tableName);   // LazyFrame/Series → понятная ошибка
        if (type.IsPandas) return Pandas.Read(frame, tableName);

        // Обратная совместимость: pandas-подобный объект (подкласс, geopandas, modin, test double)
        if (frame.HasAttr("columns") && frame.HasAttr("dtypes")) return Pandas.Read(frame, tableName);

        throw new InvalidOperationException(
            $"'{tableName}' must be a pandas.DataFrame or polars.DataFrame, got {type}");
    }
}
```

### A.2. `PolarsFrameReader`

```csharp
internal sealed class PolarsFrameReader : FrameReaderBase, IFrameReader
{
    public FrameKind Kind => FrameKind.Polars;

    public DataTable Read(PyObject frame, string tableName)
    {
        var type = PythonTypeInfo.From(frame);
        if (type.Name == "LazyFrame")
            throw new InvalidOperationException(
                $"'{tableName}' is a polars.LazyFrame, materialize it in the script: result = <expr>.collect()");
        if (type.Name != "DataFrame")
            throw new InvalidOperationException(
                $"'{tableName}' must be a polars.DataFrame, got {type}");

        var table = new DataTable(tableName);
        var sourceNames = ReadStringList(frame.GetAttr("columns"));      // columns.tolist()
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columnValues = new object[sourceNames.Length][];

        for (var i = 0; i < sourceNames.Length; i++)
        {
            using (PyObject series = frame.InvokeMethod("get_column", new PyString(sourceNames[i])))
            using (PyObject dtypeObject = series.GetAttr("dtype"))
            {
                var dtype = dtypeObject.ToString();                      // "Datetime(time_unit='us', time_zone=None)"
                var converter = GetColumnConverter(sourceNames[i], dtype, PolarsDtypes.IsForbidden(dtype));

                table.Columns.Add(new DataColumn(MakeColumnName(sourceNames[i], i, usedNames))
                {
                    AllowDBNull = true,
                    DataType = converter.TargetType
                });

                columnValues[i] = ReadColumnValues(series, converter, dtype);
            }
        }

        FillRows(table, columnValues);
        return table;
    }

    static object[] ReadColumnValues(PyObject series, IColumnConverter converter, string dtype)
    {
        if (converter is DateTimeConverter || converter is TimeSpanConverter)
        {
            var nanosecondsPerUnit = PolarsDtypes.GetNanosecondsPerUnit(dtype);   // Date → нс в сутках

            try
            {
                using (PyObject physical = series.InvokeMethod("to_physical"))
                    return converter.ConvertValues(
                        ScaleToNanoseconds(ToObjectArray(physical), nanosecondsPerUnit));
            }
            catch (PythonException)
            {
                // Несовместимая версия polars / extension dtype: читаем python date/datetime/timedelta
            }
        }

        return converter.ConvertValues(ToObjectArray(series));
    }

    /// <summary>series.to_list() → object[] (тот же приём, что в pandas-пути)</summary>
    static object[] ToObjectArray(PyObject series)
    {
        using (PyObject list = series.InvokeMethod("to_list"))
            return (object[])(dynamic)list;
    }
}
```

Замечание: tz-aware значения (`time_zone != None`) приходят из `to_physical()` как epoch UTC, поэтому после
`ConvertValues` дополнительно применяется `DateTime.SpecifyKind(value, DateTimeKind.Utc)` (по Q8) — иначе
`DateTimeConverter` вернёт `Kind = Unspecified`, как для pandas.

### A.3. `DtypeUnits`, запрещённые типы и правка реестра

```csharp
// Pike.PythonClient64/FrameReaders/DtypeUnits.cs
internal enum TemporalKind { None, Date, Datetime, Duration, Time }

internal static class DtypeUnits
{
    /// <summary>Наносекунд в сутках: физическое значение polars.Date — дни от 1970-01-01</summary>
    public const long NanosecondsPerDay = 86_400L * 1000 * 1000 * 1000;

    public static TemporalKind GetTemporalKind(string dtype)
    {
        switch (SupportedTypes.NormalizeDtype(dtype))
        {
            case "Date": return TemporalKind.Date;
            case "Datetime": return TemporalKind.Datetime;      // pandas: datetime64/datetime/timestamp
            case "Duration": return TemporalKind.Duration;      // pandas: timedelta64/timedelta
            case "Time": return TemporalKind.Time;
            default: return TemporalKind.None;
        }
    }

    /// <summary>Наносекунд в единице dtype: polars time_unit='ns|us|ms', pandas [ns|us|ms|s|m|h|D]</summary>
    public static long GetNanosecondsPerUnit(string dtype)
    {
        if (GetTemporalKind(dtype) == TemporalKind.Date) return NanosecondsPerDay;

        switch (GetTimeUnit(dtype) ?? "ns")
        {
            case "ns": return 1;
            case "us": return 1_000;
            case "ms": return 1_000_000;
            case "s": return 1_000_000_000;
            case "m": return 60L * 1_000_000_000;
            case "h": return 3600L * 1_000_000_000;
            case "D": return NanosecondsPerDay;
            default: return 1;
        }
    }

    /// <summary>Значение time_unit в polars-dtype или null</summary>
    public static string GetTimeUnit(string dtype) => Extract(dtype, "time_unit='") ?? ExtractBracketUnit(dtype);

    /// <summary>True для Datetime с time_zone (epoch UTC)</summary>
    public static bool IsTimeZoneAware(string dtype)
    {
        var timeZone = Extract(dtype, "time_zone=");
        return timeZone != null && timeZone != "None";
    }
}
```

```csharp
// Pike.PythonClient64/FrameReaders/PolarsDtypes.cs
internal static class PolarsDtypes
{
    // DataTable не умеет эти типы однозначно: в strict-режиме — NotSupportedException
    static readonly HashSet<string> Forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "List", "Array", "Struct", "Map", "Object", "Unknown", "Extension", "Int128", "UInt128"
    };

    public static bool IsForbidden(string dtype) => Forbidden.Contains(SupportedTypes.NormalizeDtype(dtype));
}
```

`Extract(dtype, prefix)` и `ExtractBracketUnit(dtype)` — небольшие утилиты разбора строки dtype
(перенос текущих `GetDtypeUnit`/`GetNanosecondsPerUnit` из `PythonDataQuery.cs:471-510`).

Правка `SupportedTypes.NormalizeDtype` (после среза `[`):

```csharp
// polars: Datetime(time_unit='us', time_zone=None) → Datetime, Decimal(precision=38, scale=2) → Decimal,
// List(String) → List, Enum(categories=['a', 'b']) → Enum
var parenthesis = normalized.IndexOf('(');
if (parenthesis > 0) normalized = normalized.Substring(0, parenthesis);
```

Дополнение `SupportedTypes.CreateDefault` (сравнение ключей регистронезависимо, поэтому `Datetime`
уже покрыт ключом `datetime`, а `Boolean`/`Int64`/`Float64`/`String` — pandas-именами):

```csharp
Add(dateTime, "Date");                                  // polars.Date → DateTime
Add(timeSpan, "Duration", "Time");                      // polars.Duration/Time → TimeSpan
Add(stringConverter, "Utf8", "Categorical", "Enum", "Null");
Add(doubleConverter, "Float16");                        // Decimal — по Q3
Add(new BinaryConverter(), "Binary");                   // по Q5
```

### A.4. Тестовая инфраструктура

```csharp
// Pike.PythonClient.Test/TestEnvironment.cs
/// <summary>True if the 'polars' package is importable in the configured python runtime</summary>
public static bool HasPolars
{
    get
    {
        if (!IsPythonAvailable) return false;
        EnsureProviderOpen();

        using (Py.GIL())
        {
            try
            {
                using (Py.Import("polars")) return true;
            }
            catch (PythonException)
            {
                return false;
            }
        }
    }
}

/// <summary>Marks the test as inconclusive when polars is not installed</summary>
public static void SkipIfNoPolars()
{
    if (!HasPolars)
        Assert.Inconclusive("polars is not installed in the python environment (uv pip install polars)");
}
```

```python
"""TestScript04: polars result, no pandas conversion."""

import base64
import gzip
import json

import polars as pl

query_text: str | None = globals().get('query', None)
query_params: dict = globals().get('params', {}) or {}

result: pl.DataFrame = pl.DataFrame({
    'No': [1, 2, 3],
    'Name': ['Olivia', 'Ava', 'Isabella'],
    'Value': [0.0, 0.6931471805599453, 1.0986122886681098],
    'Created': [
        pl.datetime(2020, 1, 1, 12, 30, 15, 123456, time_unit='us'),
        None,
        pl.datetime(1960, 1, 1, 0, 0, 0, 0, time_unit='ns'),
    ],
    'Flag': [True, None, False],
})

if query_text is not None:
    # Power Query "table to script": gzip + base64 JSON → polars
    raw_data: bytes = gzip.decompress(base64.b64decode(query_text))
    result = pl.DataFrame(json.loads(raw_data))
    result = result.with_columns(
        (pl.col('Value') * query_params.get('factor', 1)).alias('Value'))
```

Первая проверка интеграционного теста (этап 2):

```csharp
[TestMethod]
public void PolarsCoreTypes()
{
    const string code = @"import polars as pl

result = pl.DataFrame({
    'Int8': [1, 2],
    'UInt32': [1, 2],
    'Float64': [1.5, None],
    'Flag': [True, None],
    'Text': ['a', None],
})";

    var table = PythonDataQuery.RunScript(code);

    Assert.AreEqual(typeof(int), table.Columns["Int8"].DataType);
    Assert.AreEqual(typeof(int), table.Columns["UInt32"].DataType);
    Assert.AreEqual(typeof(double), table.Columns["Float64"].DataType);
    Assert.AreEqual(typeof(bool), table.Columns["Flag"].DataType);
    Assert.AreEqual(typeof(string), table.Columns["Text"].DataType);
    Assert.AreEqual(DBNull.Value, table.Rows[1]["Float64"]);
    Assert.AreEqual(DBNull.Value, table.Rows[1]["Flag"]);
    Assert.AreEqual(DBNull.Value, table.Rows[1]["Text"]);
}
```

### A.5. Окружения, CI и матрица

```powershell
# локальная установка polars в существующее окружение
. .\scripts\test\Initialize-PythonEnvironment.ps1 -Packages numpy,pandas,polars

# прогон только polars-тестов (без polars в окружении — Inconclusive)
.\scripts\test\Invoke-Tests.ps1 -Category Python -Filter 'FullyQualifiedName~PolarsTypesTests'
```

```powershell
# scripts/test/Invoke-VersionMatrix.ps1 — новая комбинация
@{
    Name     = 'py311-polars1'
    Python   = '3.11'
    Packages = @('numpy', 'pandas', 'polars')
    Comment  = 'polars 1.x, pandas 3.x'
}
```

```yaml
# .github/workflows/ci.yml
- name: Install Python packages
  run: python -m pip install --upgrade pip numpy pandas polars

- name: Integration tests (Python + polars)
  run: ./scripts/test/Invoke-Tests.ps1 -Category Python -Configuration Release -NoBuild -TimeoutSeconds 900
```

---

## Приложение B. Порядок выполнения (чек-лист)

- [x] Этап 1: вынести ридеры, `-Category All` → 154/154 без правок тестов (pandas-путь не изменился)
- [x] Этап 2: фабрика + `PolarsFrameReader` (базовые типы) + `PolarsCoreTypes`/`PolarsAdoParity`
- [x] Этап 3: `NormalizeDtype`/`DtypeUnits`/темпоральные типы + `PolarsTemporalTypes`
- [x] Этап 4: краевые типы и политики (Q3–Q6, Q11) + соответствующие тесты
- [x] Этап 5: `PolarsTypesTests`, `TestScript04.py`, `HasPolars`, polars в CI и матрице
- [x] Этап 6: README/`docs/power-query.md`/CHANGELOG/счётчики, `planPolars.md` §10 добавлен
- [ ] Smoke: Power Query/Excel возвращает polars-кадр на установленном провайдере
      (требует `install.ps1` под администратором: GAC-копия должна быть обновлена)
- [ ] Отложено: `PowerQuery - 05 - Polars.xlsx`, polars-сценарий в `samples`

# PythonClient

ADO.NET x64 data provider that uses a Python script as the data source and converts the `pandas.DataFrame`
result into a `DataTable` / `DbDataReader`. Built on [pythonnet](https://github.com/pythonnet/pythonnet)
and designed for in-process consumers such as Power Query / Excel, DataAdapter-based code and desktop tools.

## Table of contents

- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Script sources](#script-sources)
- [Connection string](#connection-string)
- [Parameters](#parameters)
- [Supported DataFrame column types](#supported-dataframe-column-types)
- [Limitations](#limitations)
- [Security](#security)
- [Troubleshooting](#troubleshooting)
- [Build and test](#build-and-test)
- [Examples](#examples)
- [License](#license)

## Requirements

| Component | Requirement |
|---|---|
| .NET | .NET Framework **4.8**, process must be **x64** |
| pythonnet | **3.0.5** (`Python.Runtime.dll` is deployed with the provider) |
| Python | Python **3.9 – 3.13 x64**, e.g. CPython or Anaconda (3.7/3.8 are not tested); `python3xx.dll` must match the process bitness |
| Python packages | `numpy`, `pandas` (pandas **1.5 – 3.1**, numpy **1.26 – 2.x**) in the same environment |
| Excel | 64-bit Excel is required for Power Query scenarios (the provider is x64-only) |

The provider does not install Python: `PYTHONDLL` must point to an existing `python3xx.dll`.

## Installation

Recommended (PowerShell, elevated):

```powershell
dotnet build PythonClient.sln -c Release
.\scripts\install.ps1          # copies files to %ProgramFiles%\Pike\PythonClient64 and registers machine.config
.\scripts\uninstall.ps1        # removes the registration and the files
```

`scripts\install.ps1` writes the registration into the 64-bit `machine.config` of .NET Framework 4.x and creates a
backup `machine.config.bak-<utc>`. The registration uses a version-independent type name
(`Pike.PythonClient64.Data.PythonProviderFactory, Pike.PythonClient64`), so a provider upgrade does not break it.

Manual registration (x64 `machine.config`):

```xml
<configuration>
  <system.data>
    <DbProviderFactories>
      <add name="Python x64 Data Provider"
           invariant="Pike.PythonClient64.Data.PythonProviderFactory"
           description=".Net Framework Data Provider for Python x64"
           type="Pike.PythonClient64.Data.PythonProviderFactory, Pike.PythonClient64" />
    </DbProviderFactories>
  </system.data>
</configuration>
```

The legacy Visual Studio Installer project (`PythonClient64Setup\PythonClient64Setup.vdproj`) requires a separate
VS extension and is deprecated, use `scripts\install.ps1` instead.

### Build artifacts and NuGet package

```powershell
.\scripts\pack.ps1            # Release build → artifacts\
```

`artifacts\` contains:

- `Pike.PythonClient64.<version>.nupkg` + `.snupkg` — package with `lib\net48` assemblies, XML documentation,
  README and the `pythonnet 3.0.5` dependency (for in-process scenarios, no `machine.config` change);
- `Pike.PythonClient64\` — flat folder (`Pike.PythonClient64.dll/.xml/.pdb`, `Python.Runtime.dll`) used by
  `scripts\install.ps1` and for manual deployment to Power Query / Excel.

The version is defined once in `Directory.Build.props` (semver) and reflected in the assembly attributes,
the NuGet package and `CHANGELOG.md`.

## Quick start

```csharp
using System;
using System.Data;
using System.IO;
using Pike.PythonClient64.Data;

const string script = @"import numpy as np
import pandas as pd

params = globals()['params'] if 'params' in globals() else {}
query_text = globals()['query'] if 'query' in globals() else None

result = pd.DataFrame({
    'StringColumn': ['Pike', None, 'Amol'],
    'BoolColumn': [True, True, False],
    'FloatColumn': [123.456, np.nan, 456.789],
    'IntColumn': [123456, 456789, 789123],
    'TimeDeltaColumn': [np.timedelta64(10, 'h'), None, np.timedelta64(12, 'h')],
    'DateTimeColumn': [np.datetime64('2020-01-01'), None, np.datetime64('2021-06-15')]
})";

var builder = new PythonConnectionStringBuilder
{
    PythonDll = @"C:\Python311\python311.dll",
    PythonPath = @"C:\Python311\Lib;C:\Python311\Lib\site-packages;C:\Python311\DLLs"
};

using (var connection = new PythonConnection { ConnectionString = builder.ConnectionString })
{
    connection.Open();

    using (var command = connection.CreateCommand())
    {
        command.CommandText = script;                     // command text is passed to python as 'query'
        command.Parameters.Add(new PythonParameter { ParameterName = "limit", Value = 10 });

        var table = new DataTable();
        using (var reader = command.ExecuteReader()) table.Load(reader);

        Console.WriteLine(connection.ServerVersion);       // python version, e.g. 3.11.5
        Console.WriteLine(table.Rows.Count);
    }
}
```

## Script sources

1. **Command text as a script** — when `FILE` is not specified in the connection string, `CommandText` is executed
   as Python code; the same text is available inside the script as the `query` variable.
2. **Script file** — `FILE=<path>` in the connection string: the file content is executed, `CommandText` is passed
   to the script as the `query` variable. Files are read as UTF-8 (BOM is detected) and cached until the file changes.

The script must assign a `pandas.DataFrame` to the `result` variable (the name can be changed with
`PythonScriptContext.ResultName`). Missing `result` raises `KeyNotFoundException`; a non-DataFrame result raises
`InvalidOperationException`.

## Connection string

| Key | Required | Description |
|---|---|---|
| `PYTHONDLL` | yes | Full path to `python3xx.dll`. The provider validates that the file exists |
| `PYTHONPATH` | no | Additional `sys.path` entries separated by `;`. **Must be quoted** when it contains `;` |
| `FILE` | no | Full path to the Python script file. When omitted, the command text is used as a script |

Example (note the quotes around `PYTHONPATH`):

```
PYTHONDLL=C:\Python311\python311.dll;PYTHONPATH="C:\Python311\Lib;C:\Python311\Lib\site-packages";FILE=C:\Scripts\Report.py
```

- Keywords are case-insensitive (`PYTHONDLL` == `pythondll`), values keep their original case.
- Unsupported keyword → `KeyNotFoundException`; malformed string → `ArgumentException`.
- An empty value (`KEY=`) is treated as "key is not set"; an empty connection string means "no settings".
- Paths from `PYTHONHOME` / `PYTHONPATH` environment variables are added to `sys.path` as well
  (see `PythonDataProvider.DefaultPathComponents`); user paths are inserted at the beginning of `sys.path`.

## Parameters

Parameters are passed to the script as the `params` dictionary. `@name` and `:name` prefixes are trimmed.

| .NET value | Python value |
|---|---|
| `null`, `DBNull.Value` | `None` |
| `bool` | `bool` |
| `int`, `long`, `short`, `byte` | `int` |
| `double`, `float` | `float` |
| `decimal` | `float` (Python has no decimal type, so precision may be lost) |
| `string` | `str` |
| `DateTime` (Kind = Local) | `numpy.datetime64[ns]` (converted to UTC) |
| `DateTime` (Kind = Utc/Unspecified) | `numpy.datetime64[ns]` (as is) |
| `DateTimeOffset` | `numpy.datetime64[ns]` (UTC) |
| `TimeSpan` | `numpy.timedelta64[ns]` |
| `Guid` | `str` |
| `byte[]` | `bytes` |

Only `ParameterDirection.Input` is supported: `Output`, `InputOutput` and `ReturnValue` raise `NotSupportedException`.

## Supported DataFrame column types

| pandas dtype | .NET type | Notes |
|---|---|---|
| `bool`, `boolean` | `bool` | `pd.NA` → `DBNull` |
| `int8`, `int16`, `int32`, `uint8`, `uint16`, `uint32` | `int` | out of range / missing → `DBNull` |
| `int64`, `uint64` | `long` | missing → `DBNull` |
| `float32`, `float64` | `double` | `np.nan` → `DBNull` |
| `datetime64[ns]` and tz-aware `datetime64[ns, UTC]` | `DateTime` | NaN-free values only; `NaT` → `DBNull`; dates before 1970 are supported |
| `timedelta64[ns]` | `TimeSpan` | `NaT` → `DBNull`, out of range → `DBNull` |
| `object`, `str`, `string` (`string[python]`) | `string` | `None` → `DBNull` |
| `category` | `string` | values are converted to their string representation |

`Int8/Int16/Int32/Int64`, `UInt*`, `Float*` (nullable pandas extension dtypes) are mapped to the same .NET types.

Rules:

- unknown dtype → `NotSupportedException` (`Column '<name>' has unsupported dtype '<dtype>'`). Set
  `PythonDataQuery.StrictColumnTypes = false` to fall back to string conversion;
- duplicated column labels → `NotSupportedException` (duplicated labels cannot be read as a `Series`);
- characters `[`, `]`, `.` are replaced in the column name (`a.b` → `a_b`), colliding names get a `_2`, `_3` suffix;
- custom converters can be registered: `SupportedTypes.Register(new MyConverter(), "dtype_name")`.

## Limitations

- **One Python engine per process.** `PythonConnection.Open()` initializes it once; `Close()` only releases the
  reference. `PythonDataProvider.ShutdownOnLastClose = false` keeps the interpreter alive until the process exits
  (recommended for Excel/Power Query). Two connections can use different path components, but not different
  `python*.dll` files.
- **Transactions are not supported** (`BeginTransaction` → `NotSupportedException`), `ChangeDatabase` is not supported.
- **`CommandTimeout` is not supported** (`NotSupportedException` when > 0): a Python script cannot be interrupted
  reliably, `Cancel()` does nothing. Use script-level timeouts.
- **Threading.** The GIL is acquired for every operation, so parallel commands are serialized by Python itself.
  `PythonConnection`/`PythonCommand` instances are not thread-safe; use one command per thread.
- **Memory.** The whole `DataFrame` is materialized into a `DataTable` before the reader is returned; there is no
  streaming for very large results.
- **`CommandBehavior`**: `SchemaOnly`, `SingleRow` and `CloseConnection` are supported.
- **`ExecuteNonQuery`** returns the number of result rows; scripts without `result` give `0`.

## Security

Python code is executed **inside the consumer process** (Excel, your application) with the permissions of that
process. There is no sandbox, no resource limits and no signature validation of scripts. Only run scripts you trust,
and do not build connection strings from untrusted input. See [SECURITY.md](SECURITY.md).

## Power Query

`AdoDotNet.Query` accepts the provider invariant name, so Power Query / Excel can use Python scripts as a data source:

```fsharp
let
    query = AdoDotNet.Query(
        "Pike.PythonClient64.Data.PythonProviderFactory",
        "PYTHONDLL=C:\Python311\python311.dll;PYTHONPATH=""C:\Python311\Lib;C:\Python311\Lib\site-packages""",
        "import pandas as pd
result = pd.DataFrame({'Value': [1, 2, 3]})")
in
    query
```

Excel must be 64-bit, and the provider must be registered in `machine.config` (see [Installation](#installation)).
An Excel table can be transferred to the script as a compressed base64 string in the query text and decoded in Python;
see `docs/power-query.md` and the examples in `Pike.PythonClient.Test\Excel`.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `InvalidOperationException: PythonDllPath is required` | connection string has no `PYTHONDLL`, or the connection string was set after `Open()` |
| `FileNotFoundException: Can't find python*.dll file` | wrong `PYTHONDLL` path or 32-bit DLL used in a 64-bit process |
| `Python engine is already initialized with '<dll>'` | the process already uses another Python runtime; open the connection with the same `PYTHONDLL` |
| `ArgumentException: Формат строки инициализации не соответствует спецификации` | `PYTHONPATH` contains `;` without quotes |
| `KeyNotFoundException: ... assign result ...` | the script does not define the `result` variable |
| `InvalidOperationException: 'result' must be a pandas.DataFrame` | the script assigns a list/dict instead of a DataFrame |
| `NotSupportedException: Column '...' has unsupported dtype '...'` | unsupported dtype; cast it in the script or disable `PythonDataProvider`/`PythonDataQuery.StrictColumnTypes` |
| `DbProviderFactories.GetFactory` fails in Power Query | provider is not registered in `machine.config` (run `install.ps1`) or 32-bit Excel is used |
| `NotSupportedException: CommandTimeout is not supported` | set `CommandTimeout = 0` |

## Build and test

Both projects are SDK-style (`net48` + `x64`), so `dotnet build` works without a solution restore step:

```powershell
dotnet build PythonClient.sln -c Release
```

The helper scripts live in `scripts\`:

| Script | Purpose |
|---|---|
| `scripts\test\Initialize-PythonEnvironment.ps1` | creates a local Python environment with [uv](https://docs.astral.sh/uv/) (`.venv`, numpy, pandas) and sets `PYTHONNET_TEST_DLL`/`PYTHONPATH` |
| `scripts\test\Invoke-Tests.ps1` | builds (optional) and runs the tests with a timeout, prints failed tests from the trx report |
| `scripts\test\Invoke-VersionMatrix.ps1` | runs the integration tests against Python 3.9/3.11/3.13 and pandas 1.5/2.2/3.1 (`.venv-matrix\*`) |
| `scripts\test\PythonEnvironment.ps1` | shared helpers (create environment, resolve `python3xx.dll` and `site-packages`) |
| `scripts\smoke\Test-ProviderSmoke.ps1` | 40 smoke checks on the built assembly (no Python required) |
| `scripts\pack.ps1` | Release build + `dotnet pack` into `artifacts\` (nupkg/snupkg and a flat deploy folder) |

```powershell
# unit tests: do not need Python (85 tests)
.\scripts\test\Invoke-Tests.ps1 -Category Unit

# integration tests: create the environment once, then run (46 tests)
. .\scripts\test\Initialize-PythonEnvironment.ps1
.\scripts\test\Invoke-Tests.ps1 -Category Python

# everything
.\scripts\test\Invoke-Tests.ps1 -Category All

# version matrix (creates one environment per combination)
.\scripts\test\Invoke-VersionMatrix.ps1

# quick sanity check without Python
.\scripts\smoke\Test-ProviderSmoke.ps1 -Configuration Release
```

`PYTHONNET_TEST_DLL` is the only configuration the tests need: without it, the `Python` tests are reported as
inconclusive instead of failing. The provider registration for the tests is taken from
`Pike.PythonClient.Test\app.config`, so no `machine.config` change or administrator rights are required.

### Tested versions

Every combination below passes the whole integration suite (46 tests) on Windows x64, .NET Framework 4.8:

| Python | pandas | numpy | Result |
|---|---|---|---|
| 3.9.25 | 1.5.3 | 1.26.4 | 46 / 46 |
| 3.11.16 | 2.2.3 | 1.26.4 | 46 / 46 |
| 3.11.16 | 3.1.0 | 2.4.6 | 46 / 46 |
| 3.13.15 | 3.1.0 | 2.5.4 | 46 / 46 |

pandas 3 changed the default datetime resolution to microseconds (`datetime64[us]`), the provider reads the unit from
the dtype and supports `[ns]`, `[us]`, `[ms]`, `[s]`, `[m]`, `[h]` and `[D]`.

### Continuous integration

`.github/workflows/ci.yml` runs on `windows-latest`:

- **build-and-test** — build (Release), Unit tests, integration tests (Python 3.11 + numpy/pandas), smoke checks,
  `scripts/pack.ps1`, upload of the nupkg and the deployable binaries;
- **version-matrix** — the same integration suite for the Python/pandas combinations from
  `scripts/test/Invoke-VersionMatrix.ps1` (uv installs the interpreters).

## Examples

- C# tests: `Pike.PythonClient.Test\Tests`
- Excel / Power Query workbooks: `Pike.PythonClient.Test\Excel`
- Provider registration tests: `Pike.PythonClient.Test\Tests\Unit\DataProviderInstallationUnit.cs`

## License

[MIT](LICENSE)

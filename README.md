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
- [polars support](#polars-support)
- [Limitations](#limitations)
- [Security](#security)
- [Troubleshooting](#troubleshooting)
- [Build and test](#build-and-test)
- [Examples](#examples)
- [License](#license)

## Requirements

| Component | Requirement |
|---|---|
| .NET | **.NET Framework 4.8** (Power Query / Excel host), process must be **x64** |
| pythonnet | **3.0.5** (`Python.Runtime.dll` is deployed with the provider) |
| Python | Python **3.9 – 3.13 x64**, e.g. CPython or Anaconda (3.7/3.8 are not tested); `python3xx.dll` must match the process bitness |
| Python packages | `numpy`, `pandas` (pandas **1.5 – 3.1**, numpy **1.26 – 2.x**) in the same environment; **`polars` is optional** (1.x/2.x, needed only for polars results) |
| Excel | 64-bit Excel is required for Power Query scenarios (the provider is x64-only) |

The provider does not install Python: `PYTHONDLL` must point to an existing `python3xx.dll`.
The installation itself needs administrator rights: `Pike.PythonClient64.dll` and `Python.Runtime.dll` are published
into the GAC and the provider is registered in the x64 `machine.config`.

## Installation

### From a GitHub release (recommended)

1. Download `Pike.PythonClient64-<version>.zip` from
   [Releases](https://github.com/answering007/PythonClient/releases/latest) (`.zip.sha256` contains the checksum).
2. Extract the archive to a local folder — do not run the files from inside the ZIP.
3. Run `install.cmd` and confirm the UAC prompt. The wrapper unblocks the extracted files (Mark-of-the-Web), reports
   the prerequisites, installs and verifies the GAC registration.
4. Restart Excel. `verify.cmd` checks the installation later (read-only, no administrator rights),
   `uninstall.cmd` removes it.

The archive contains `lib\` (the six installed files), `scripts\` (`install.ps1`, `uninstall.ps1`,
`Verify-Installation.ps1`), `samples\power-query` with ready-made workbooks, `samples\Python` with the scripts those
workbooks run, and `SHA256SUMS.txt`. Details: [docs/installation.md](docs/installation.md).

### From source (developers)

```powershell
dotnet build PythonClient.sln -c Release
.\scripts\install.ps1               # copies bin\Release\net48, updates the GAC and machine.config
.\scripts\Verify-Installation.ps1   # read-only check: files, machine.config, GAC entries, resolved type
.\scripts\uninstall.ps1             # removes the registration, the GAC entries and the files
```

Run the scripts from an elevated **64-bit Windows PowerShell 5.1** (`System.EnterpriseServices.Internal.Publish`,
which updates the GAC, does not exist in PowerShell 7); `gacutil` and the Windows SDK are not required.
`scripts\build-distribution.ps1` builds the end-user ZIP (version taken from `Directory.Build.props`);
`.github/workflows/release.yml` publishes it together with the NuGet package on every `v*` tag.
`Verify-Installation.ps1` needs no administrator rights and changes nothing: it compares the installed files with
`bin\Release`, checks the `machine.config` record, and loads the provider from the GAC in a new process (a stale GAC
entry or a Debug build installed by mistake is reported with the failing check name).

`scripts\install.ps1` publishes `Pike.PythonClient64.dll` and its `pythonnet` dependency `Python.Runtime.dll` into the
global assembly cache, then writes the registration into the 64-bit `machine.config` of .NET Framework 4.x and creates
a backup `machine.config.bak-<utc>`. Both the provider and pythonnet are strong-named, so the GAC accepts them.

The registration contains the **fully qualified** type name
(`Pike.PythonClient64.Data.PythonProviderFactory, Pike.PythonClient64, Version=2.0.0.0, Culture=neutral,
PublicKeyToken=8c0beba75960bb80`): the runtime resolves an assembly outside the host application base (the Excel
folder) only by a full name, and a partial name is never looked up in the GAC. `Python.Runtime.dll` has to be in the
GAC for the same reason — the dependencies of a GAC assembly are probed in the GAC. `AssemblyVersion` is fixed for the
whole 2.x line, so a provider upgrade keeps the existing record valid; only the GAC entries are replaced.
`install.ps1` verifies the result in a new process and fails if the type is not loaded from `GAC_64`.

Debug builds use `AssemblyVersion` **2.0.0.1** (see `Directory.Build.props`), so the provider installed on the machine
does not shadow the developer build: a strong-named assembly is always resolved from the GAC first. The test project
patches the provider record of its copied `app.config` to the same version, so `DbProviderFactories` and the tests use
the freshly built assembly. Release keeps 2.0.0.0 — the version the `machine.config` record expects.

The registration code lives in the separate `Pike.PythonClient64.Install.dll` (project `Pike.PythonClient64.Install`):
the runtime provider loaded by Excel/Power Query references only `mscorlib`, `System`, `System.Core`, `System.Data`,
`Microsoft.CSharp` and `pythonnet` — no installer or `System.Configuration.Install` dependency is pulled into the
consumer process. `scripts\install.ps1` copies both assemblies and loads the installer assembly only during
install/uninstall.

Manual registration (x64 `machine.config`):

```xml
<configuration>
  <system.data>
    <DbProviderFactories>
      <add name="Python x64 Data Provider"
           invariant="Pike.PythonClient64.Data.PythonProviderFactory"
           description=".Net Framework Data Provider for Python x64"
           type="Pike.PythonClient64.Data.PythonProviderFactory, Pike.PythonClient64, Version=2.0.0.0, Culture=neutral, PublicKeyToken=8c0beba75960bb80" />
    </DbProviderFactories>
  </system.data>
</configuration>
```

The record alone is not enough: `Pike.PythonClient64.dll` and `Python.Runtime.dll` must be present in the GAC
(`scripts\install.ps1` does both), otherwise the runtime cannot resolve the type outside the Excel folder.

There is no installer project in the solution anymore: the legacy Visual Studio Installer project (`.vdproj`, which
required a separate VS extension) was removed in 2.1.0. Installation is done by the scripts above (repository) or by
`install.cmd` from the release ZIP.

An MSI/graphical installer is **not** part of 2.1.0 and is deliberately deferred. The installation itself is only six
files, two GAC entries and one `machine.config` entry (the provider installs neither Python nor pandas), so the
release ZIP and the scripts above remain the supported path: they are transparent, exercised by CI (including the
"install from the distribution ZIP" step) and need no additional toolchain. A per-machine MSI (`msiexec /qn`, ARP
entry, repair/uninstall) will be added on top of these scripts — reusing the same registration code — once silent
enterprise deployment (GPO / SCCM / Intune) becomes a requirement. The planned shape is a
`Pike.PythonClient64.Install.Tool.exe` (install/uninstall/status), a WiX-built MSI and an optional Burn bootstrapper
for a wizard UI.

### Build artifacts and NuGet package

```powershell
.\scripts\pack.ps1            # Release build → artifacts\
```

`artifacts\` contains:

- `Pike.PythonClient64.<version>.nupkg` + `.snupkg` — **runtime only** (no installer dependency): `lib\net48` assembly,
  XML documentation, README and the `pythonnet 3.0.5` dependency;
- `Pike.PythonClient64\` — flat folder with the **net48** build (`Pike.PythonClient64.dll/.xml/.pdb`,
  `Python.Runtime.dll`) **and** `Pike.PythonClient64.Install.dll/.xml`; used as the installation payload by
  `scripts\install.ps1` (the installation publishes the assemblies into the GAC, no copy in `%ProgramFiles%`) and for
  manual deployment to a .NET host;
- `Pike.PythonClient64\Tests\Python\` — sources of the Python integration tests (`Pike.PythonClient.Test\Tests\Python`),
  published for review and debugging. The installation copies only the named assemblies and the NuGet package stays
  runtime-only, so the folder is never installed; it is uploaded as a part of the CI build artifact.

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
   to the script as the `query` variable (Power Query uses this to pass an Excel table). Files are read as UTF-8
   (BOM is detected) and cached until the file changes.

In both modes an empty `CommandText` means "no query": the `query` variable is not defined then.

The script must assign a `pandas.DataFrame` or a `polars.DataFrame` to the `result` variable (the name can be changed
with `PythonScriptContext.ResultName`). The frame type is detected automatically, see [polars support](#polars-support).
Missing `result` raises `KeyNotFoundException`; a result that is not a frame raises `InvalidOperationException`.

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

## Async API

`PythonConnection.OpenAsync`, `PythonCommand.ExecuteReaderAsync/ExecuteScalarAsync/ExecuteNonQueryAsync` and
`PythonDataQuery.RunScriptAsync` execute the Python work on a thread pool thread, so the caller thread is not blocked:

```csharp
await using (var connection = new PythonConnection { ConnectionString = connectionString })
{
    await connection.OpenAsync();

    await using (var command = connection.CreateCommand())
    {
        command.CommandText = script;
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                Console.WriteLine(reader.GetInt64(0));
    }
}
```

A Python script cannot be interrupted, so the cancellation token only prevents the execution from starting
(a pre-cancelled token returns a cancelled task). Use script-level timeouts for long computations.

## Custom converters

Any dtype can be mapped by a custom `IColumnConverter`; the registry is process-wide and thread-safe:

```csharp
public sealed class CategoryConverter : IColumnConverter
{
    public string PythonTypeName => "category";
    public Type TargetType => typeof(string);

    public object[] ConvertValues(object[] pythonValues)
    {
        var result = new object[pythonValues.Length];
        for (var i = 0; i < pythonValues.Length; i++)
            result[i] = pythonValues[i] == null ? (object)DBNull.Value : "cat:" + pythonValues[i];

        return result;
    }
}

SupportedTypes.Register(new CategoryConverter(), "category");   // replaces the default mapping
SupportedTypes.Reset();                                          // restores the defaults
```

`SupportedTypes.TryGetConverter(dtype, out converter)` checks the mapping without an exception,
`NormalizeDtype` removes byte-order markers and dtype parameters (`datetime64[ns, UTC]` → `datetime64`).
Unknown dtype is rejected with `NotSupportedException` unless `PythonDataQuery.StrictColumnTypes = false`.

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

## polars support

`result` may be a `polars.DataFrame`: the provider detects the frame type by the type of the object and reads it
directly, without converting it to pandas (`to_pandas()` is not needed):

```python
import polars as pl

result = pl.DataFrame({
    'No': [1, 2, 3],
    'Name': ['Olivia', 'Ava', 'Isabella'],
    'Value': [0.0, 0.6931471805599453, 1.0986122886681098],
})
```

The detection does not import pandas or polars, so both packages stay optional. pandas results work exactly as before;
an object that is neither pandas nor polars but has `columns` and `dtypes` is still read by the pandas reader.

| polars dtype | .NET type | Notes |
|---|---|---|
| `Boolean` | `bool` | `null` → `DBNull` |
| `Int8`, `Int16`, `Int32`, `UInt8`, `UInt16`, `UInt32` | `int` | out of range / `null` → `DBNull` |
| `Int64`, `UInt64` | `long` | `null` → `DBNull`; `UInt64` above `long.MaxValue` → `DBNull` |
| `Float16`, `Float32`, `Float64` | `double` | `NaN` and `null` → `DBNull` |
| `String`, `Utf8`, `Categorical`, `Enum` | `string` | values are read as text, not as the physical codes |
| `Date` | `DateTime` | midnight of the date |
| `Datetime` (`ns`/`us`/`ms`, with or without a time zone) | `DateTime` | read from the physical value; a time zone aware value keeps the UTC clock time (`DataColumn` does not preserve `DateTime.Kind`) |
| `Duration` | `TimeSpan` | `null` → `DBNull` |
| `Time` | `TimeSpan` | time of day |
| `Decimal` | `double` | transferred as `double`, the same way as a decimal parameter |
| `Binary` | `byte[]` | `null` → `DBNull` |
| `Null` | `string` | every value is `DBNull` |
| `List`, `Array`, `Struct`, `Map`, `Object`, `Unknown`, `Extension`, `Int128`, `UInt128` | `string` only with `StrictColumnTypes = false` | `NotSupportedException` by default |

Rules:

- a `polars.LazyFrame` must be materialized in the script (`result = <lazy>.collect()`), a `polars.Series` is not
  accepted — both are reported with the actual type and a hint;
- empty frames keep the schema (typed columns, `0` rows), a frame without columns gives an empty table.

## Limitations

- **One Python engine per process.** `PythonConnection.Open()` initializes it once; `Close()` only releases the
  reference. `PythonDataProvider.ShutdownOnLastClose = false` keeps the interpreter alive until the process exits
  (recommended for Excel/Power Query). Two connections can use different path components, but not different
  `python*.dll` files.
- **Transactions are not supported** (`BeginTransaction` → `NotSupportedException`), `ChangeDatabase` is not supported.
- **`CommandTimeout` is accepted and ignored**: a Python script cannot be interrupted reliably, so the value (Power
  Query / Excel sets it for `AdoDotNet.Query`) never aborts the execution and `Cancel()` does nothing. Use timeouts
  inside the script.
- **Threading.** The GIL is acquired for every operation, so parallel commands are serialized by Python itself.
  `PythonConnection`/`PythonCommand` instances are not thread-safe; use one command per thread.
- **Memory.** The whole `DataFrame` is always materialized into a `DataTable` before the reader is returned, so a large
  result must fit into memory. Reduce the frame in the script (`LIMIT`, `.head(n)`, aggregation) before it is
  transferred.
- **`CommandBehavior`**: `SchemaOnly`, `SingleRow` and `CloseConnection` are supported.
- **`ExecuteNonQuery`** returns the number of result rows; scripts without `result` give `0`.
- **polars is optional**: it is installed only when the script uses it (`pip install polars`), and the provider never
  imports it. Without polars the polars integration tests are reported as inconclusive.
- **polars nested and 128-bit types** (`List`, `Struct`, `Array`, `Int128`, ...) have no `DataTable` representation:
  they raise `NotSupportedException` unless `PythonDataQuery.StrictColumnTypes = false`, then the string
  representation is used. Cast the column in the script to keep typed data.

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
| `ValueError: The 'query' variable is not defined in the global scope` | the command text is empty, or Excel loaded an older provider from the GAC: rebuild, rerun `install.ps1` and restart Excel |
| `DbProviderFactories.GetFactory` fails in Power Query | the provider is missing from the GAC or from `machine.config` (run `install.ps1`; `uninstall.ps1` removes both), or 32-bit Excel is used |
| `ConfigurationErrorsException: ... InvariantName ... already exists` | the provider is registered twice: in `machine.config` (by `install.ps1`) and in the application `app.config`; keep one of them, or add `<remove invariant="..." />` before `<add>` (see `Pike.PythonClient.Test\app.config`) |
| Local changes have no effect after installation | a strong-named assembly is resolved from the GAC before the application folder: rerun `install.ps1` (or `uninstall.ps1`) to replace the GAC copy. Debug builds are not affected — they use `AssemblyVersion` 2.0.0.1 and the test `app.config` is patched to it automatically |
| Is the installed provider the current build? | run `.\scripts\Verify-Installation.ps1` (read-only, no administrator rights): it compares the installed files with `bin\Release` (SHA256), checks the `machine.config` record and both GAC entries, loads the provider in a new process and reports the installed build without polars support |
| Uninstall without the distribution archive | the registration code (`Pike.PythonClient64.Install`) is intentionally not published in the GAC, so keep the release ZIP: run `uninstall.cmd` from the extracted archive, or pass `-SourcePath <folder with lib>` to `uninstall.ps1` (the repository layout is detected automatically) |

## Build and test

Both projects are SDK-style (`net48` + `x64`), so `dotnet build` works without a solution restore step:

```powershell
dotnet build PythonClient.sln -c Release
```

The helper scripts live in `scripts\`:

| Script | Purpose |
|---|---|
| `scripts\test\Initialize-PythonEnvironment.ps1` | creates a local Python environment with [uv](https://docs.astral.sh/uv/) (`.venv`, numpy, pandas) — the tests find it automatically |
| `scripts\test\Invoke-Tests.ps1` | builds (optional) and runs the tests with a timeout, prints failed tests from the trx report; uses `<repo>\.venv` when no runtime is configured |
| `scripts\test\Invoke-VersionMatrix.ps1` | runs the integration tests against Python 3.9/3.11/3.13 and pandas 1.5/2.2/3.1 (`.venv-matrix\*`) |
| `scripts\test\PythonEnvironment.ps1` | shared helpers (create environment, resolve `python3xx.dll` and `site-packages`) |
| `scripts\smoke\Test-ProviderSmoke.ps1` | 41 smoke checks on the built assembly (no Python required) |
| `scripts\Verify-Installation.ps1` | read-only check of an installed machine (no administrator rights): installed files vs `bin\<Configuration>`, `machine.config` record, GAC entries, the type resolved in a new process, polars support in the installed build |
| `scripts\build-distribution.ps1` | builds the end-user distribution ZIP (`Pike.PythonClient64-<version>.zip`): payload, `install.cmd`/`verify.cmd`/`uninstall.cmd`, scripts, Power Query samples and `SHA256SUMS.txt` |
| `scripts\pack.ps1` | Release build + `dotnet pack` into `artifacts\` (nupkg/snupkg and a flat deploy folder) |

```powershell
# unit tests: do not need Python (95 tests)
.\scripts\test\Invoke-Tests.ps1 -Category Unit

# integration tests (81 tests): create the environment once, it is picked up automatically
# polars is optional; add it to the environment to run the polars tests (17 more)
.\scripts\test\Initialize-PythonEnvironment.ps1 -Packages numpy,pandas,polars
.\scripts\test\Invoke-Tests.ps1 -Category Python

# or point the run at any other virtual environment
.\scripts\test\Invoke-Tests.ps1 -Category Python -EnvironmentPath .\.venv-matrix\py311-pandas22

# everything
.\scripts\test\Invoke-Tests.ps1 -Category All

# version matrix (creates one environment per combination)
.\scripts\test\Invoke-VersionMatrix.ps1

# quick sanity check without Python
.\scripts\smoke\Test-ProviderSmoke.ps1 -Configuration Release
```

The python runtime is resolved in this order, so no manual configuration is required when the local `.venv`
exists:

1. `PYTHONNET_TEST_DLL` (and `PYTHONPATH`) — set by CI, the scripts and `-PythonDll`/`-SitePackages`;
2. `PYTHONNET_TEST_VENV` — path to a virtual environment (`pyvenv.cfg` + `python3xx.dll` + numpy/pandas are read
   from it), useful for debugging in Visual Studio Test Explorer;
3. `<repository root>\.venv` — the default local environment created by `Initialize-PythonEnvironment.ps1`.

The same discovery is implemented twice: in `PythonRuntimeLocator`/`TestEnvironment` for the test assembly (works
in Test Explorer and `vstest.console` without any environment variable) and in `PythonEnvironment.ps1` for the
scripts. Without a runtime the `Python` tests are reported as inconclusive instead of failing. Python 3.7–3.10
environments must be selected through the scripts (`-PythonDll`/`-SitePackages`): pythonnet starts the interpreter
from a non-Python host, and CPython before 3.11 cannot locate its `Lib` folder in that process.

The provider registration for the tests is taken from `Pike.PythonClient.Test\app.config`, so no
`machine.config` change or administrator rights are required.

### Tested versions

The suite has **176 tests** (95 unit without Python + 81 integration). Every combination below passes the whole
integration suite on Windows x64:

| Runtime | Python | pandas | numpy | polars | Result |
|---|---|---|---|---|---|
| .NET Framework 4.8 | 3.9.25 | 1.5.3 | 1.26.4 | — | 64 / 64 (17 inconclusive) |
| .NET Framework 4.8 | 3.11.16 | 2.2.3 | 1.26.4 | — | 64 / 64 (17 inconclusive) |
| .NET Framework 4.8 | 3.11.16 | 3.1.0 | 2.4.6 | 2.1.0 | 81 / 81 |
| .NET Framework 4.8 | 3.13.15 | 3.1.0 | 2.5.4 | — | 64 / 64 (17 inconclusive) |

pandas 3 changed the default datetime resolution to microseconds (`datetime64[us]`), the provider reads the unit from
the dtype and supports `[ns]`, `[us]`, `[ms]`, `[s]`, `[m]`, `[h]` and `[D]`. Missing values are mapped to `DBNull`
regardless of the representation (`None`, `pd.NA`, `pd.NaT`, `np.nan`, `nan` of the inferred string dtype).

polars is optional: without the package the 17 polars tests are reported as inconclusive, the pandas suite stays green.

### Sample (.NET Framework 4.8)

`samples\Pike.PythonClient64.Sample` is a minimal x64 console host that uses the async API and prints the result table:

```powershell
. .\scripts\test\Initialize-PythonEnvironment.ps1
$env:PYTHONNET_SAMPLE_DLL = $env:PYTHONNET_TEST_DLL
$env:PYTHONNET_SAMPLE_PATH = $env:PYTHONPATH
samples\Pike.PythonClient64.Sample\bin\Release\net48\Pike.PythonClient64.Sample.exe
```

The sample is also executed by CI, so the async path and the DataFrame conversion are covered at runtime on every
build.

### Continuous integration

`.github/workflows/ci.yml` runs on `windows-latest`:

- **build-and-test** — build (Release), Unit tests, integration tests (Python 3.11 + numpy/pandas/polars), smoke checks,
  `scripts/pack.ps1`, upload of the nupkg and the deployable binaries;
- **version-matrix** — the same integration suite for the Python/pandas/polars combinations from
  `scripts/test/Invoke-VersionMatrix.ps1` (uv installs the interpreters).

## Examples

- C# tests: `Pike.PythonClient.Test\Tests`
- Excel / Power Query workbooks: `Pike.PythonClient.Test\Excel`
- Provider registration tests: `Pike.PythonClient.Test\Tests\Unit\DataProviderInstallationUnit.cs`

## License

[MIT](LICENSE)

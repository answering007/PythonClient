# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **Power Query / Excel: `CommandTimeout` was rejected.** The `AdoDotNet` connector sets `CommandTimeout` on the command,
  and the provider failed with `NotSupportedException: CommandTimeout is not supported, python scripts can't be
  interrupted reliably` before running the script. The value is now accepted (and validated as an ADO.NET property:
  negative values throw `ArgumentOutOfRangeException`) and ignored — a python script cannot be interrupted, so the
  execution is never aborted and `Cancel()` stays a no-op.
- The test `app.config` removes the inherited `machine.config` record before adding its own: with the provider installed
  on the machine, `DbProviderFactories` rejected the duplicated invariant name and the configuration could not be loaded.

### Removed

- **Chunked (streaming) DataFrame reading** — breaking change. `PythonCommand.Streaming`, `PythonCommand.StreamingChunkSize`
  and the public `PythonDataReader` are removed together with the internal `DataFrameCursor`/`DataFrameColumn`: the
  provider always materializes the whole `DataFrame` into a `DataTable` and `ExecuteReader` returns a standard
  `DataTableReader`. `CommandBehavior.SchemaOnly`/`SingleRow`/`CloseConnection`, typed getters, `GetSchemaTable()` and
  `DataTable.Load` keep working through the standard reader.
  *Migration:* drop the `Streaming`/`StreamingChunkSize` assignments (a plain `ExecuteReader()` is enough); replace
  `GetDataTypeName` (python dtype) with `GetFieldType` (managed type); reduce large results in the script
  (`LIMIT`, `.head(n)`, aggregation) instead of relying on bounded memory.

### Changed

- The whole frame is read in a single pass: every column is converted once, without the `iloc[start:stop]` slices the
  chunk reader needed, so a full materialization copies less data.
- Test suite: **152 tests** (90 unit + 62 integration), including the new `ScriptVariablesTests` (`query`/`params`
  transfer: command text vs script file, an always-defined `params` dictionary, prefix trimming, execution isolation)
  and `ScriptContextUnit` (context defaults, variable names).

## [2.1.0] - 2026-10-29

### Added

- **GAC installation.** `scripts\install.ps1` now publishes `Pike.PythonClient64.dll` and its `pythonnet` dependency
  `Python.Runtime.dll` into the global assembly cache (`GAC_64`) and verifies in a new process that the registered type
  is resolved from there; `scripts\uninstall.ps1` removes the GAC entries as well, so an uninstall no longer leaves a
  phantom provider behind. No `gacutil`/Windows SDK is needed (`System.EnterpriseServices.Internal.Publish`).
  Both scripts require an elevated 64-bit Windows PowerShell 5.1 and check it.
- `scripts\test\Invoke-InstallE2E.ps1` — installation end-to-end check (install → `DbProviderFactories.GetFactory`
  from the GAC in a new process → uninstall), executed by CI.
- `Pike.PythonClient64.Install.dll` is strong-named now (same `key.snk`), so every shipped assembly has a
  `PublicKeyToken`.

### Added (optional roadmap stage 4)

- **Async API.** `PythonConnection.OpenAsync`, `PythonCommand.ExecuteReaderAsync`/`ExecuteScalarAsync`/
  `ExecuteNonQueryAsync` and `PythonDataQuery.RunScriptAsync` execute the Python work on a thread pool thread, so the
  caller is never blocked. A cancellation token prevents the execution from starting (Python scripts are not
  interruptible).
- **Streaming large results.** `PythonCommand.Streaming` (+ `StreamingChunkSize`, default 4096) returns a new
  `PythonDataReader` that reads the DataFrame in chunks (`DataFrameCursor`): bounded memory, standard ADO.NET schema
  (`GetSchemaTable`, `DataTable.Load`), typed getters, `SchemaOnly`/`SingleRow`/`CloseConnection` support.
  The materialization path also uses the cursor now, so a table is filled chunk by chunk with a reused row buffer.
- **Custom converters** are documented with a complete `IColumnConverter` example
  (`SupportedTypes.Register`/`Reset`, `TryGetConverter`, `NormalizeDtype`).
- `samples\Pike.PythonClient64.Sample` — minimal .NET Framework 4.8 x64 host (async + streaming), executed by CI.
- Test suite grown to **145 tests** (85 unit + 60 integration); version matrix re-validated:
  Python 3.9/3.11/3.13 × pandas 1.5/2.2/3.1 — 60/60 in every combination.

### Changed

- **Target framework: `net48` only.** The `net8.0-windows` target (an experiment in this release cycle) was dropped:
  the provider is distributed as a single `lib\net48` assembly for the x64 Power Query / Excel and desktop hosts.
  `dotnet build` outputs stay in `bin\<Configuration>\net48`, so the deployment scripts are unchanged.
- **`machine.config` registration uses the fully qualified type name**
  (`Pike.PythonClient64.Data.PythonProviderFactory, Pike.PythonClient64, Version=2.0.0.0, Culture=neutral,
  PublicKeyToken=8c0beba75960bb80`) instead of the assembly simple name: only a full name is resolved from the GAC,
  a partial name is probed in the host application base (the Excel folder) only.
  `AssemblyVersion` is fixed to `2.0.0.0` for the whole 2.x line, so the record stays valid across upgrades.
  *Migration:* run `uninstall.ps1` (or `install.ps1` directly) once on the machines that were registered by an
  earlier build; the record is rewritten in place and the assemblies are published into the GAC.

### Removed

- `PythonDataQuery.Query`, `PythonDataQuery.Parameters` and `PythonDataQuery.Reset()` (deprecated in 2.0.0) together
  with the internal ambient context: the execution state is passed explicitly via `PythonScriptContext` /
  `PythonCommand`, so there is no hidden static state and no `AsyncLocal` fallback anymore;
  `RunScript(scriptText)` uses a new default context.
- The legacy Visual Studio Installer project `PythonClient64Setup\PythonClient64Setup.vdproj` (deprecated in 2.0.0,
  required a separate VS extension) is removed from the solution: deployment and registration are done by
  `scripts\install.ps1` / `uninstall.ps1` only.

### Fixed

- **Shutdown from a non-owner thread.** `EndAllowThreads` can be called only in the thread that saved the state;
  with an async host the close happened on another thread and deadlocked. The finalization is now skipped in that
  case instead of hanging, and `PlatformNotSupportedException` from `PythonEngine.Shutdown()` (pythonnet 3.0.5 uses
  `BinaryFormatter`, which is unavailable in .NET 8+ hosts) is handled as a safe fallback.
- **`NaN` in the inferred string dtype (pandas 3).** Missing values of the default string dtype (`nan`) were
  transferred as the literal `"nan"`/`"не число"` string; `NaN` is now treated as a missing value and becomes
  `DBNull` (also covers `np.nan` in object columns).

## [2.0.0] - 2026-10-29

### Fixed

- **Connection string (P0-1).** Values are no longer upper-cased: the custom parser was replaced by
  `DbConnectionStringBuilder.ConnectionString`, keyword lookup is case-insensitive.
  *Migration:* quote values that contain `;` — `PYTHONPATH="C:\Py\Lib;C:\Py\Lib\site-packages"`.
- **Parameter collection (P0-2, P0-4).** `CopyTo` copies all items from the requested index;
  `Parameters[name] = value` replaces the existed parameter instead of throwing; `Insert` honours the index;
  `Contains(object)`/`IndexOf(object)`/`Remove(object)` are safe for `null` and non-parameter values;
  `IsSynchronized` is `false`; the enumerator returns parameters instead of key/value pairs.
- **Parameters (P0-3).** `PythonParameter.Value = null` and `DbType = DbType.X` are allowed;
  `DBNull` no longer breaks `Size` calculation.
- **Provider registration (P0-5).** `DbProviderFactories` is created inside `system.data`, the existed record is
  updated, `type` is version-independent, `machine.config` is backed up and replaced atomically (UTF-8 without BOM).
  The dependency on `Microsoft.Build.Utilities.v4.0` was removed.
- **GIL and engine lifecycle (P0-6, P0-7).** The GIL is acquired per operation instead of being held for the whole
  application; the engine is reference counted, so closing one connection no longer shuts it down for others;
  `PythonDataProvider.ShutdownOnLastClose` allows keeping the interpreter alive until the process exits.
- **Date/time converters (P0-8).** Dates before 1970, `NaT` and out-of-range `timedelta64` values no longer throw:
  `NaT` and out-of-range values are returned as `DBNull`.
- **DataFrame conversion (P0-9).** dtype is normalized (`datetime64[ns, UTC]`, `string[python]`, nullable `Int64`…),
  unknown dtype raises `NotSupportedException` instead of the silent string conversion, column names are normalized
  and uniquified, duplicated labels are reported, the result object is disposed, `np.nan` becomes `DBNull`.
- **ADO.NET contracts (P1-1…P1-5).** `CommandBehavior` (`SchemaOnly`, `SingleRow`, `CloseConnection`) is supported,
  `CommandTimeout` and transactions raise `NotSupportedException`, `ExecuteNonQuery` allows scripts without `result`,
  script files are read as UTF-8 and cached, parameters support `null`/`decimal`/`Guid`/`TimeSpan`/`DateTimeOffset`,
  `@`/`:` prefixes are trimmed, execution context is per call (`PythonScriptContext`).

### Fixed (found by integration testing with Python 3.11.16 / numpy 2.4.6 / pandas 3.1.0)

- **GIL and worker threads (P0-6).** `PythonEngine.Initialize()` keeps the GIL in the initializing thread, so any
  Python call from another thread blocked forever. The provider releases it with `BeginAllowThreads()` and restores
  the saved thread state before the finalization: scripts, parallel connections and `RunScript` from a worker thread
  now work.
- **datetime64/timedelta64 resolution (P0-8).** pandas 3 stores datetimes with microsecond resolution
  (`datetime64[us]`), the converter assumed nanoseconds, so values were shifted (e.g. `1960-01-01` came back as
  `1969-12-28`). The unit is now read from the dtype (`[ns]`, `[us]`, `[ms]`, `[s]`, `[m]`, `[h]`, `[D]`) and scaled.
- **Non-string column labels (P0-9).** Columns were selected by a string name, so DataFrames with default integer
  labels (`0`, `1`, …) failed with `KeyError`. The series are selected by the original label, the string name is used
  for the `DataColumn` only.
- **pandas NA/NaT in object and string columns (P0-9).** `pd.NA` was transferred as the literal `"<NA>"` string;
  `pd.NA`/`pd.NaT` are now treated as missing values and become `DBNull`.
- **Engine shutdown from another thread (P0-7).** `PythonEngine.Shutdown()` is no longer called while the provider
  lock is held, and a `Close()` of an already closed connection does not release someone else's reference.

### Added (validation tooling)

- `scripts/test/Initialize-PythonEnvironment.ps1` — local Python environment via `uv` (numpy, pandas) and the
  `PYTHONNET_TEST_DLL`/`PYTHONPATH` variables used by the tests.
- `scripts/test/Invoke-Tests.ps1` — build/test runner with a timeout (a hung interpreter no longer blocks the build),
  failures are reported from the trx file.
- `scripts/test/Invoke-VersionMatrix.ps1` and `scripts/test/PythonEnvironment.ps1` — version matrix runner
  (Python 3.9/3.11/3.13 × pandas 1.5/2.2/3.1) and shared environment helpers.
- `scripts/smoke/Test-ProviderSmoke.ps1` — 40 checks on the built assembly without Python.
- `scripts/install.ps1` / `scripts/uninstall.ps1` were moved from the repository root to `scripts\`.
- `PythonDataProvider.ReferenceCount` for diagnostics.
- `Pike.PythonClient.Test\app.config` registers the provider, so `DbProviderFactories` tests need no
  `machine.config` change.

### Validated (version matrix)

Every combination below passes the whole integration suite (46 tests) on Windows x64, .NET Framework 4.8:

| Python | pandas | numpy | Result |
|---|---|---|---|
| 3.9.25 | 1.5.3 | 1.26.4 | 46 / 46 |
| 3.11.16 | 2.2.3 | 1.26.4 | 46 / 46 |
| 3.11.16 | 3.1.0 | 2.4.6 | 46 / 46 |
| 3.13.15 | 3.1.0 | 2.5.4 | 46 / 46 |

Performance baseline: `TestLargeDataFrame` (90 000 rows × 6 columns) takes ~0.3 s per run (Debug build, warm engine).

### Changed (release engineering)

- **Installer code extracted (P2-2).** `DataProviderInstallationBase`, `DbProviderInstallation` and `InstallerMain`
  moved from the runtime assembly into the new project/assembly `Pike.PythonClient64.Install`. The runtime assembly
  loaded by Excel/Power Query now references only `mscorlib`, `System`, `System.Core`, `System.Data`,
  `Microsoft.CSharp` and `pythonnet` — `System.Configuration.Install` (and `System.Xml`) are no longer pulled into the
  consumer process. The NuGet package contains the runtime only; the deploy folder and `scripts/install.ps1` /
  `uninstall.ps1` use both assemblies.
- Version bumped to **2.0.0**: the release contains breaking changes (see above).
- Shared build metadata moved to `Directory.Build.props` (`Version`, `AssemblyVersion`, `FileVersion`, company,
  product, description, MIT license, tags); assembly attributes are generated by the SDK, `Properties\AssemblyInfo.cs`
  now contains only `ComVisible`/`Guid` (the test project has no `AssemblyInfo.cs`).
- `dotnet pack` produces `Pike.PythonClient64.<version>.nupkg` (+ `.snupkg` symbols) with `lib\net48`,
  XML documentation, README and the `pythonnet 3.0.5` dependency; `scripts/pack.ps1` builds Release into `artifacts\`
  together with a flat deploy folder for `scripts/install.ps1`.

### Added

- `PythonScriptContext` and `PythonDataQuery.RunScript(scriptText, context)` for isolated script execution.
- `SupportedTypes.Register/Reset/TryGetConverter/NormalizeDtype` — extensible dtype registry.
- `PythonDataProvider.ShutdownOnLastClose`, `PythonDataProvider.Shutdown()`, `PythonDataProvider.Version` caching.
- `PythonDataQuery.StrictColumnTypes` (default `true`).
- Unit test suite (`TestCategory=Unit`, 85 tests) that does not require Python.
- `install.ps1` / `uninstall.ps1`, `LICENSE`, `SECURITY.md`, `CHANGELOG.md`, `.editorconfig`, CI workflow.

### Changed

- Projects migrated to SDK-style (`Microsoft.NET.Sdk`) with `PackageReference`; `packages.config` removed.
- Release builds keep portable PDB files (the post-build PDB deletion was removed).
- The legacy Visual Studio Installer project (`.vdproj`) is deprecated in favour of `install.ps1` (removed in 2.1.0).

### Deprecated

- `PythonDataQuery.Query`, `PythonDataQuery.Parameters`, `PythonDataQuery.Reset()`: use `PythonScriptContext` or
  `PythonCommand` properties (removed in 2.1.0).

### Breaking changes

- `PythonConnectionStringBuilder` rejects values with unquoted `;` (spec-compliant).
- `SupportedTypes.Values` is `IReadOnlyDictionary<string, IColumnConverter>` (was mutable `IDictionary`).
- `SupportedTypes` is a static class.
- `float` columns use `DBNull` instead of `double.NaN` for `np.nan`.
- Unknown dtype raises `NotSupportedException` (previously converted to string silently).
- Duplicated DataFrame column labels raise `NotSupportedException`.
- `PythonParameterCollection` enumeration yields `PythonParameter` items (not `KeyValuePair`).
- `CommandTimeout > 0` raises `NotSupportedException`.

## [1.2] - 2025

- .NET Framework 4.8 target, XML documentation for `DbProviderFactory`, README example.
